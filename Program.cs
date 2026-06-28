using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

Console.WriteLine("OTD 环境一键极速部署工具 By LinHouYu\n");

string baseDir = AppDomain.CurrentDomain.BaseDirectory;
string dotNetPath = Path.Combine(baseDir, "net8-installer.exe");
string otdExtractDir = Path.Combine(baseDir, "OpenTabletDriver");

using var client = new HttpClient();
client.DefaultRequestHeaders.Add("User-Agent", "OTD-Auto-Installer");

string otdDownloadUrl = "", otdFileName = "OTD.zip", versionTag = "";
try
{
    var jsonString = await client.GetStringAsync("https://api.github.com/repos/OpenTabletDriver/OpenTabletDriver/releases/latest");
    var json = JsonNode.Parse(jsonString ?? "{}");
    var targetAsset = json?["assets"]?.AsArray().FirstOrDefault(a => a?["name"]?.ToString().EndsWith("win-x64.zip") == true);
    if (targetAsset == null) throw new Exception("未找到 win-x64 压缩包");

    otdDownloadUrl = targetAsset["browser_download_url"]?.ToString() ?? "";
    otdFileName = targetAsset["name"]?.ToString() ?? "OTD.zip";
    versionTag = json?["tag_name"]?.ToString() ?? "";
}
catch
{
    Console.WriteLine("=> 抓取版本失败，请检查网络。");
    return;
}

if (!File.Exists(dotNetPath))
{
    Console.WriteLine("[1/7] 下载 .NET 8 桌面运行环境...");
    await DownloadAsync("https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.28/windowsdesktop-runtime-8.0.28-win-x64.exe", dotNetPath);
}

string[] existingZips = Directory.GetFiles(baseDir, "OpenTabletDriver*win-x64.zip");
string otdZipPath = existingZips.Length > 0 ? existingZips[0] : Path.Combine(baseDir, otdFileName);

if (!Directory.Exists(otdExtractDir) && !File.Exists(otdZipPath))
{
    Console.WriteLine($"\n[2/7] 下载 OTD {versionTag}...");
    string[] proxies = ["https://gh-proxy.com/", "https://github.akams.cn/", "https://ghproxy.net/", ""];
    foreach (var proxy in proxies)
    {
        try
        {
            bool isDirect = string.IsNullOrEmpty(proxy);
            Console.WriteLine(isDirect ? "-> 尝试 GitHub 原站直连..." : $"-> 尝试加速节点: {proxy}");
            await DownloadAsync(proxy + otdDownloadUrl, otdZipPath, isDirect ? 0 : 5);
            break;
        }
        catch { Console.WriteLine("-> 连接失败或超时，自动切换..."); }
    }
}

if (File.Exists(otdZipPath) && !Directory.Exists(otdExtractDir))
{
    Console.WriteLine("\n[3/7] 正在解压 OTD 并处理嵌套目录...");
    ZipFile.ExtractToDirectory(otdZipPath, otdExtractDir);
    File.Delete(otdZipPath);

    string[] nestedDirs = Directory.GetDirectories(otdExtractDir, "OpenTabletDriver*win-x64");
    if (nestedDirs.Length > 0)
    {
        string nestedDir = nestedDirs[0];
        foreach (string file in Directory.GetFiles(nestedDir)) File.Move(file, Path.Combine(otdExtractDir, Path.GetFileName(file)), true);
        foreach (string dir in Directory.GetDirectories(nestedDir)) Directory.Move(dir, Path.Combine(otdExtractDir, Path.GetFileName(dir)));
        Directory.Delete(nestedDir);
    }
}

Console.WriteLine("\n[4/7] 部署环境 (安装 .NET 8 并配置 OTD 便携模式)...");
if (File.Exists(dotNetPath))
{
    try
    {
        using var process = Process.Start(new ProcessStartInfo { FileName = dotNetPath, Arguments = "/install /quiet /norestart", UseShellExecute = true, Verb = "runas" });
        process?.WaitForExit();
        if (File.Exists(dotNetPath)) File.Delete(dotNetPath);
    }
    catch { Console.WriteLine("-> 用户取消了 .NET 8 安装，或未提供管理员权限。"); }
}

string batPath = Path.Combine(otdExtractDir, "convert_to_portable.bat");
if (File.Exists(batPath))
{
    Console.WriteLine("-> 正在执行 convert_to_portable.bat...");
    using var batProcess = new Process { StartInfo = new ProcessStartInfo { FileName = batPath, WorkingDirectory = otdExtractDir, CreateNoWindow = true, UseShellExecute = false, RedirectStandardInput = true } };
    batProcess.Start();
    batProcess.StandardInput.WriteLine();
    batProcess.StandardInput.Close();
    batProcess.WaitForExit();

    string migratedSettings = Path.Combine(otdExtractDir, "userdata", "settings.json");
    if (File.Exists(migratedSettings)) File.Delete(migratedSettings);
}

Console.WriteLine("\n[5/7] 检查 VMulti 驱动状态 (防止蓝屏)...");
try
{
    bool needInstallVMulti = true;
    using (var checkProc = new Process())
    {
        checkProc.StartInfo.FileName = "cmd.exe";
        checkProc.StartInfo.Arguments = "/c driverquery | findstr /i vmulti";
        checkProc.StartInfo.UseShellExecute = false;
        checkProc.StartInfo.RedirectStandardOutput = true;
        checkProc.StartInfo.CreateNoWindow = true;
        checkProc.Start();
        string output = checkProc.StandardOutput.ReadToEnd();
        checkProc.WaitForExit();

        if (!string.IsNullOrWhiteSpace(output))
        {
            Console.WriteLine("-> 检测到系统已安装 VMulti 驱动，跳过重复安装。");
            needInstallVMulti = false;
        }
    }

    if (needInstallVMulti)
    {
        Console.WriteLine("-> 准备下载安装 VMulti 驱动...");
        var vJsonString = await client.GetStringAsync("https://api.github.com/repos/X9VoiD/vmulti-bin/releases/latest");
        var vJson = JsonNode.Parse(vJsonString ?? "{}");
        var vAsset = vJson?["assets"]?.AsArray().FirstOrDefault(a => a?["name"]?.ToString() == "VMulti.Driver.zip");
        if (vAsset != null)
        {
            string vZip = Path.Combine(baseDir, "VMulti.Driver.zip");
            string vExt = Path.Combine(baseDir, "VMultiTemp");
            string vDlUrl = vAsset["browser_download_url"]?.ToString() ?? "";

            foreach (var proxy in new[] { "https://gh-proxy.com/", "https://github.akams.cn/", "https://ghproxy.net/", "" })
            {
                try { await DownloadAsync(proxy + vDlUrl, vZip, string.IsNullOrEmpty(proxy) ? 0 : 5); break; } catch { }
            }

            if (File.Exists(vZip))
            {
                if (Directory.Exists(vExt)) Directory.Delete(vExt, true);
                ZipFile.ExtractToDirectory(vZip, vExt);
                File.Delete(vZip);
                string vBat = Path.Combine(vExt, "install_hiddriver.bat");
                if (File.Exists(vBat))
                {
                    Console.WriteLine("-> 请求管理员权限安装驱动 (如有黑框提示请按回车)...");
                    try
                    {
                        using var vp = Process.Start(new ProcessStartInfo { FileName = vBat, WorkingDirectory = vExt, UseShellExecute = true, Verb = "runas" });
                        vp?.WaitForExit();
                    }
                    catch { Console.WriteLine("-> 用户取消了驱动安装。"); }
                }
                try { Directory.Delete(vExt, true); } catch { }
            }
        }
    }
}
catch (Exception ex) { Console.WriteLine($"-> 驱动检查异常: {ex.Message}"); }

Console.WriteLine("\n[6/7] 安装插件与注入预设...");
await InstallOtdPluginAsync("OpenTabletDriver", "TabletDriverFilters", "HawkuFilters.zip", "HawkuFilters", "Ported hawku/TabletDriver interpolators");
await InstallOtdPluginAsync("Kuuuube", "VoiDPlugins", "VMultiMode.zip", "VMultiMode", "Classic VMulti Output Mode");

string daemonPath = Path.Combine(otdExtractDir, "OpenTabletDriver.Daemon.exe");
string settingsPath = Path.Combine(otdExtractDir, "userdata", "settings.json");

if (File.Exists(daemonPath))
{
    Console.WriteLine("-> 正在后台唤醒 OTD，等待其生成数位板配置文件...");
    using var daemonProcess = Process.Start(new ProcessStartInfo { FileName = daemonPath, WorkingDirectory = otdExtractDir, CreateNoWindow = true, UseShellExecute = false });

    int waitCounter = 0;
    while (!File.Exists(settingsPath) && waitCounter < 10)
    {
        await Task.Delay(1000);
        waitCounter++;
    }

    await Task.Delay(2000);
    try { daemonProcess?.Kill(); } catch { }
    await Task.Delay(1000);

    if (File.Exists(settingsPath))
    {
        try
        {
            var json = JsonNode.Parse(await File.ReadAllTextAsync(settingsPath));

            string anyTemplateStr = """
            {
              "OutputMode": { "Path": "VoiDPlugins.OutputMode.VMultiRelativeMode", "Settings": [], "Enable": true },
              "Filters": [
                { "Path": "OpenTabletDriver.Plugin.NoiseReduction", "Settings": [ { "Property": "Samples", "Value": 3 }, { "Property": "DistanceThreshold", "Value": 0.2 } ], "Enable": true },
                { "Path": "TabletDriverFilters.Hawku.Smoothing", "Settings": [ { "Property": "Frequency", "Value": 1000.0 }, { "Property": "Latency", "Value": 0.0 } ], "Enable": true }
              ],
              "RelativeModeSettings": { "XSensitivity": 12.0, "YSensitivity": 12.0, "RelativeRotation": 0.0, "RelativeResetDelay": "00:00:00.1000000" },
              "Bindings": {
                "TipActivationThreshold": 1.0,
                "TipButton": { "Path": "VoiDPlugins.OutputMode.VMultiButtonHandler", "Settings": [ { "Property": "Button", "Value": "Left" } ], "Enable": true },
                "EraserActivationThreshold": 1.0,
                "EraserButton": { "Path": "OpenTabletDriver.Desktop.Binding.AdaptiveBinding", "Settings": [ { "Property": "Binding", "Value": "Eraser" } ], "Enable": true },
                "PenButtons": [
                  { "Path": "VoiDPlugins.OutputMode.VMultiButtonHandler", "Settings": [ { "Property": "Button", "Value": "Right" } ], "Enable": true },
                  { "Path": "VoiDPlugins.OutputMode.VMultiButtonHandler", "Settings": [ { "Property": "Button", "Value": "Middle" } ], "Enable": true }
                ]
              }
            }
            """;

            var anyTemplate = JsonNode.Parse(anyTemplateStr);

            if (json?["Profiles"]?.AsArray() is { } profiles && anyTemplate is not null)
            {
                foreach (var profile in profiles.OfType<JsonObject>())
                {
                    profile["OutputMode"] = anyTemplate["OutputMode"]?.DeepClone();
                    profile["Filters"] = anyTemplate["Filters"]?.DeepClone();
                    profile["RelativeModeSettings"] = anyTemplate["RelativeModeSettings"]?.DeepClone();

                    if (profile["Bindings"] is JsonObject targetBindings && anyTemplate["Bindings"] is JsonObject sourceBindings)
                    {
                        foreach (var kvp in sourceBindings)
                        {
                            if (targetBindings.ContainsKey(kvp.Key))
                            {
                                targetBindings[kvp.Key] = kvp.Value?.DeepClone();
                            }
                        }
                    }
                }
                await File.WriteAllTextAsync(settingsPath, json.ToJsonString(new() { WriteIndented = true }));
                Console.WriteLine("-> 预设智能注入成功！(已保留当前数位板专属配置)");
            }
        }
        catch (Exception ex) { Console.WriteLine($"-> 预设注入失败: {ex.Message}"); }
    }
    else { Console.WriteLine("-> 依然未找到 settings.json，跳过注入。"); }
}

Console.WriteLine("\n[7/7] 配置桌面快捷方式与隐藏自启 (强力清洗历史注册表)...");
string uxPath = Path.Combine(otdExtractDir, "OpenTabletDriver.UX.Wpf.exe");

if (File.Exists(uxPath))
{
    try
    {
        string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        string commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);

        string desktopLnk = Path.Combine(desktopDir, "OpenTabletDriver.lnk");
        string localLnk = Path.Combine(otdExtractDir, "OpenTabletDriver_AutoStart.lnk");

        string[] oldShortcuts = {
            Path.Combine(userStartup, "OpenTabletDriver.UX.Wpf.exe.lnk"),
            Path.Combine(commonStartup, "OpenTabletDriver.UX.Wpf.exe.lnk"),
            Path.Combine(userStartup, "OpenTabletDriver_AutoStart.lnk"),
            Path.Combine(commonStartup, "OpenTabletDriver_AutoStart.lnk"),
            Path.Combine(userStartup, "OpenTabletDriver.lnk"),
            Path.Combine(commonStartup, "OpenTabletDriver.lnk")
        };
        foreach (var sc in oldShortcuts) { if (File.Exists(sc)) File.Delete(sc); }

        string psScript = $$"""
        $wshell = New-Object -ComObject WScript.Shell
        
        $desk = $wshell.CreateShortcut('{{desktopLnk}}')
        $desk.TargetPath = '{{uxPath}}'
        $desk.WorkingDirectory = '{{otdExtractDir}}'
        $desk.WindowStyle = 1
        $desk.Save()
        
        $local = $wshell.CreateShortcut('{{localLnk}}')
        $local.TargetPath = '{{uxPath}}'
        $local.WorkingDirectory = '{{otdExtractDir}}'
        $local.WindowStyle = 7
        $local.Save()
        
        $paths = @(
            'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run',
            'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run',
            'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run',
            'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder',
            'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder'
        )
        foreach ($p in $paths) {
            if (Test-Path $p) {
                (Get-Item $p).GetValueNames() | Where-Object { $_ -match 'OpenTabletDriver' } | ForEach-Object {
                    Remove-ItemProperty -Path $p -Name $_ -ErrorAction SilentlyContinue
                }
            }
        }
        """;

        using var psProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile",
                RedirectStandardInput = true,
                CreateNoWindow = true,
                UseShellExecute = false
            }
        };
        psProcess.Start();
        psProcess.StandardInput.WriteLine(psScript);
        psProcess.StandardInput.Close();
        psProcess.WaitForExit();

        Console.WriteLine("-> 快捷方式生成与防拦截策略部署成功！");

        string startupLnk = Path.Combine(commonStartup, "OpenTabletDriver_AutoStart.lnk");
        try
        {
            File.Copy(localLnk, startupLnk, true);
            Console.WriteLine("-> 隐藏自启 (Common) 部署成功！");
        }
        catch (UnauthorizedAccessException)
        {
            startupLnk = Path.Combine(userStartup, "OpenTabletDriver_AutoStart.lnk");
            File.Copy(localLnk, startupLnk, true);
            Console.WriteLine("-> 隐藏自启 (User) 部署成功！");
        }
    }
    catch (Exception ex) { Console.WriteLine($"-> 自启部署失败: {ex.Message}"); }
}
else { Console.WriteLine("-> 未找到 OTD 主程序，跳过自启配置。"); }

Console.WriteLine("\n全部部署完成！按任意键退出...");
Console.ReadKey();


async Task InstallOtdPluginAsync(string owner, string repo, string assetName, string pluginName, string description)
{
    string pluginsDir = Path.Combine(otdExtractDir, "userdata", "Plugins");
    Directory.CreateDirectory(pluginsDir);

    try
    {
        string apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
        var jsonString = await client.GetStringAsync(apiUrl);
        var json = JsonNode.Parse(jsonString ?? "{}");
        string version = json?["tag_name"]?.ToString().TrimStart('v') ?? "1.0.0";
        var targetAsset = json?["assets"]?.AsArray().FirstOrDefault(a => a?["name"]?.ToString() == assetName);
        if (targetAsset == null) return;

        string dlUrl = targetAsset["browser_download_url"]?.ToString() ?? "";
        string zipPath = Path.Combine(pluginsDir, assetName);
        string extractDir = Path.Combine(pluginsDir, pluginName);

        foreach (var proxy in new[] { "https://gh-proxy.com/", "https://github.akams.cn/", "https://ghproxy.net/", "" })
        {
            try { await DownloadAsync(proxy + dlUrl, zipPath, string.IsNullOrEmpty(proxy) ? 0 : 5); break; } catch { }
        }

        string sha256Str = "";
        using (var sha256 = SHA256.Create())
        using (var stream = File.OpenRead(zipPath))
        {
            sha256Str = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
        ZipFile.ExtractToDirectory(zipPath, extractDir);

        var meta = new
        {
            Name = pluginName,
            Owner = owner,
            Description = description,
            PluginVersion = version,
            SupportedDriverVersion = "0.6.0.0",
            MaxSupportedDriverVersion = default(string),
            RepositoryUrl = $"https://github.com/{owner}/{repo}",
            DownloadUrl = dlUrl,
            CompressionFormat = "zip",
            SHA256 = sha256Str,
            WikiUrl = default(string),
            LicenseIdentifier = "GPL-3.0-only"
        };

        await File.WriteAllTextAsync(Path.Combine(extractDir, "metadata.json"), System.Text.Json.JsonSerializer.Serialize(meta, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        File.Delete(zipPath);
        Console.WriteLine($"-> 成功安装插件: {pluginName} (v{version})");
    }
    catch { }
}

async Task DownloadAsync(string url, string path, int timeoutSeconds = 0)
{
    using var cts = new CancellationTokenSource();
    if (timeoutSeconds > 0) cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
    using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
    response.EnsureSuccessStatusCode();

    long totalBytes = response.Content.Headers.ContentLength ?? -1L;
    using var stream = await response.Content.ReadAsStreamAsync();
    using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
    byte[] buffer = new byte[8192]; int read; long totalRead = 0;

    while ((read = await stream.ReadAsync(buffer)) > 0)
    {
        await fileStream.WriteAsync(buffer.AsMemory(0, read));
        totalRead += read;
        if (totalBytes > 0) Console.Write($"\r进度: {totalRead * 100.0 / totalBytes:F1}% [{totalRead / 1048576.0:F2} MB / {totalBytes / 1048576.0:F2} MB]   ");
    }
    Console.WriteLine();
}