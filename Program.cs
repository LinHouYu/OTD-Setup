using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

Console.WriteLine("✨ OTD 环境一键极速部署工具 ✨\n");

string baseDir = AppDomain.CurrentDomain.BaseDirectory;
string dotNetPath = Path.Combine(baseDir, "net8-installer.exe");
string otdExtractDir = Path.Combine(baseDir, "OpenTabletDriver");

using var client = new HttpClient();
client.DefaultRequestHeaders.Add("User-Agent", "OTD-Auto-Installer");

// 1. 获取 OTD 最新版本
string otdDownloadUrl = "", otdFileName = "OTD.zip", versionTag = "";
try
{
    var json = JsonNode.Parse(await client.GetStringAsync("https://api.github.com/repos/OpenTabletDriver/OpenTabletDriver/releases/latest"));
    var targetAsset = json?["assets"]?.AsArray().FirstOrDefault(a => a?["name"]?.ToString().EndsWith("win-x64.zip") == true);
    if (targetAsset == null) throw new Exception("未找到 win-x64 压缩包");
    otdDownloadUrl = targetAsset["browser_download_url"]!.ToString();
    otdFileName = targetAsset["name"]!.ToString();
    versionTag = json?["tag_name"]?.ToString() ?? "";
}
catch { return; }

// 2. 下载 .NET 8 与 OTD
if (!File.Exists(dotNetPath))
    await DownloadAsync("https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.28/windowsdesktop-runtime-8.0.28-win-x64.exe", dotNetPath);

string[] existingZips = Directory.GetFiles(baseDir, "OpenTabletDriver*win-x64.zip");
string otdZipPath = existingZips.Length > 0 ? existingZips[0] : Path.Combine(baseDir, otdFileName);

if (!Directory.Exists(otdExtractDir) && !File.Exists(otdZipPath))
{
    string[] proxies = ["https://gh-proxy.com/", "https://github.akams.cn/", "https://ghproxy.net/", ""];
    foreach (var proxy in proxies)
    {
        try { await DownloadAsync(proxy + otdDownloadUrl, otdZipPath, string.IsNullOrEmpty(proxy) ? 0 : 5); break; } catch { }
    }
}

// 3. 解压与目录扁平化
if (File.Exists(otdZipPath) && !Directory.Exists(otdExtractDir))
{
    ZipFile.ExtractToDirectory(otdZipPath, otdExtractDir);
    File.Delete(otdZipPath);
    string[] nestedDirs = Directory.GetDirectories(otdExtractDir, "OpenTabletDriver*win-x64");
    if (nestedDirs.Length > 0)
    {
        foreach (string file in Directory.GetFiles(nestedDirs[0])) File.Move(file, Path.Combine(otdExtractDir, Path.GetFileName(file)), true);
        foreach (string dir in Directory.GetDirectories(nestedDirs[0])) Directory.Move(dir, Path.Combine(otdExtractDir, Path.GetFileName(dir)));
        Directory.Delete(nestedDirs[0]);
    }
}

// 4. 环境安装与便携模式
if (File.Exists(dotNetPath))
{
    try
    {
        using var process = Process.Start(new ProcessStartInfo { FileName = dotNetPath, Arguments = "/install /quiet /norestart", UseShellExecute = true, Verb = "runas" });
        process?.WaitForExit();
        File.Delete(dotNetPath);
    }
    catch { }
}

string batPath = Path.Combine(otdExtractDir, "convert_to_portable.bat");
if (File.Exists(batPath))
{
    using var batProcess = new Process { StartInfo = new ProcessStartInfo { FileName = batPath, WorkingDirectory = otdExtractDir, CreateNoWindow = true, UseShellExecute = false, RedirectStandardInput = true } };
    batProcess.Start();
    batProcess.StandardInput.WriteLine();
    batProcess.StandardInput.Close();
    batProcess.WaitForExit();
}

// 5. 安装 VMulti 驱动
Console.WriteLine("\n[5/6] 检查/安装 VMulti 驱动...");
try
{
    var vJson = JsonNode.Parse(await client.GetStringAsync("https://api.github.com/repos/X9VoiD/vmulti-bin/releases/latest"));
    var vAsset = vJson?["assets"]?.AsArray().FirstOrDefault(a => a?["name"]?.ToString() == "VMulti.Driver.zip");
    if (vAsset != null)
    {
        string vZip = Path.Combine(baseDir, "VMulti.Driver.zip");
        string vExt = Path.Combine(baseDir, "VMultiTemp");
        foreach (var proxy in new[] { "https://gh-proxy.com/", "https://github.akams.cn/", "https://ghproxy.net/", "" })
        {
            try { await DownloadAsync(proxy + vAsset["browser_download_url"]!.ToString(), vZip, string.IsNullOrEmpty(proxy) ? 0 : 5); break; } catch { }
        }
        if (File.Exists(vZip))
        {
            if (Directory.Exists(vExt)) Directory.Delete(vExt, true);
            ZipFile.ExtractToDirectory(vZip, vExt);
            File.Delete(vZip);
            string vBat = Path.Combine(vExt, "install_hiddriver.bat");
            if (File.Exists(vBat))
            {
                try
                {
                    using var vp = Process.Start(new ProcessStartInfo { FileName = vBat, WorkingDirectory = vExt, UseShellExecute = true, Verb = "runas" });
                    vp?.WaitForExit();
                }
                catch { }
            }
            try { Directory.Delete(vExt, true); } catch { }
        }
    }
}
catch { }

// 6. 插件安装与预设动态注入
Console.WriteLine("\n[6/6] 安装插件与注入预设...");
await InstallOtdPluginAsync("OpenTabletDriver", "TabletDriverFilters", "HawkuFilters.zip", "HawkuFilters", "Ported hawku/TabletDriver interpolators");
await InstallOtdPluginAsync("Kuuuube", "VoiDPlugins", "VMultiMode.zip", "VMultiMode", "Classic VMulti Output Mode");

string daemonPath = Path.Combine(otdExtractDir, "OpenTabletDriver.Daemon.exe");
// 优先查找 userdata 目录下的 settings.json
string settingsPath = Path.Combine(otdExtractDir, "userdata", "settings.json");

if (File.Exists(daemonPath))
{
    using var daemonProcess = Process.Start(new ProcessStartInfo { FileName = daemonPath, WorkingDirectory = otdExtractDir, CreateNoWindow = true, UseShellExecute = false });
    Thread.Sleep(3000);
    daemonProcess?.Kill();

    if (!File.Exists(settingsPath)) settingsPath = Path.Combine(otdExtractDir, "settings.json");

    if (File.Exists(settingsPath))
    {
        try
        {
            var json = JsonNode.Parse(await File.ReadAllTextAsync(settingsPath));
            string myOutMode = @"{ ""Path"": ""VoiDPlugins.OutputMode.VMultiRelativeMode"", ""Settings"": [], ""Enable"": true }";
            string myFilters = @"[ { ""Path"": ""OpenTabletDriver.Plugin.NoiseReduction"", ""Settings"": [ { ""Property"": ""Samples"", ""Value"": 3 }, { ""Property"": ""DistanceThreshold"", ""Value"": 0.2 } ], ""Enable"": true }, { ""Path"": ""TabletDriverFilters.Hawku.Smoothing"", ""Settings"": [ { ""Property"": ""Frequency"", ""Value"": 1000.0 }, { ""Property"": ""Latency"", ""Value"": 0.0 } ], ""Enable"": true } ]";

            var profiles = json?["Profiles"]?.AsArray();
            if (profiles != null)
            {
                foreach (var profile in profiles)
                {
                    if (profile != null)
                    {
                        profile["OutputMode"] = JsonNode.Parse(myOutMode);
                        profile["Filters"] = JsonNode.Parse(myFilters);
                    }
                }
                await File.WriteAllTextAsync(settingsPath, json!.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch { }
    }
}

Console.WriteLine("\n✨ 全部部署完成！按任意键退出...");
Console.ReadKey();


// ==========================================
// 核心方法区
// ==========================================
async Task InstallOtdPluginAsync(string owner, string repo, string assetName, string pluginName, string description)
{
    // 修改为 userdata/Plugins 路径
    string pluginsDir = Path.Combine(otdExtractDir, "userdata", "Plugins");
    Directory.CreateDirectory(pluginsDir);

    try
    {
        string apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
        var json = JsonNode.Parse(await client.GetStringAsync(apiUrl));
        string version = json?["tag_name"]?.ToString().TrimStart('v') ?? "1.0.0";
        var targetAsset = json?["assets"]?.AsArray().FirstOrDefault(a => a?["name"]?.ToString() == assetName);
        if (targetAsset == null) return;

        string dlUrl = targetAsset["browser_download_url"]!.ToString();
        string zipPath = Path.Combine(pluginsDir, assetName);
        string extractDir = Path.Combine(pluginsDir, pluginName);

        foreach (var proxy in new[] { "https://gh-proxy.com/", "https://github.akams.cn/", "https://ghproxy.net/", "" })
        {
            try { await DownloadAsync(proxy + dlUrl, zipPath, string.IsNullOrEmpty(proxy) ? 0 : 5); break; } catch { }
        }

        string sha256Str = "";
        using (var sha256 = SHA256.Create())
        using (var stream = File.OpenRead(zipPath))
            sha256Str = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();

        if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
        ZipFile.ExtractToDirectory(zipPath, extractDir);

        var meta = new { Name = pluginName, Owner = owner, Description = description, PluginVersion = version, SupportedDriverVersion = "0.6.0.0", MaxSupportedDriverVersion = (string)null, RepositoryUrl = $"https://github.com/{owner}/{repo}", DownloadUrl = dlUrl, CompressionFormat = "zip", SHA256 = sha256Str, WikiUrl = (string)null, LicenseIdentifier = "GPL-3.0-only" };
        await File.WriteAllTextAsync(Path.Combine(extractDir, "metadata.json"), System.Text.Json.JsonSerializer.Serialize(meta, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        File.Delete(zipPath);
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