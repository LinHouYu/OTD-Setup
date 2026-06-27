using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json.Nodes;

Console.WriteLine("✨ OTD 环境一键极速部署工具 ✨\n");

string baseDir = AppDomain.CurrentDomain.BaseDirectory;
string dotNetPath = Path.Combine(baseDir, "net8-installer.exe");

using var client = new HttpClient();
// 必须设置 User-Agent，否则 GitHub API 会拒绝请求
client.DefaultRequestHeaders.Add("User-Agent", "OTD-Auto-Installer");

// ==========================================
// 1. 动态抓取最新 Release (微型爬虫)
// ==========================================
Console.WriteLine("[1/4] 正在连接 GitHub 自动抓取 OTD 最新版本...");
string apiUrl = "https://api.github.com/repos/OpenTabletDriver/OpenTabletDriver/releases/latest";
string otdDownloadUrl = string.Empty;
string otdFileName = "OTD.zip";
string versionTag = string.Empty;

try
{
    string jsonString = await client.GetStringAsync(apiUrl);
    var json = JsonNode.Parse(jsonString);
    var assets = json?["assets"]?.AsArray();

    var targetAsset = assets?.FirstOrDefault(a => a?["name"]?.ToString().EndsWith("win-x64.zip") == true);
    if (targetAsset == null) throw new Exception("未在最新 Release 中找到 win-x64 压缩包！");

    otdDownloadUrl = targetAsset["browser_download_url"]!.ToString();
    otdFileName = targetAsset["name"]!.ToString();
    versionTag = json?["tag_name"]?.ToString() ?? "未知版本";

    Console.WriteLine($"=> 成功锁定最新版本: {versionTag} ({otdFileName})");
}
catch (Exception ex)
{
    Console.WriteLine($"=> 抓取版本失败: {ex.Message}");
    Console.WriteLine("按任意键退出...");
    Console.ReadKey();
    return;
}

string otdExtractDir = Path.Combine(baseDir, "OpenTabletDriver");

// 动态检测本地是否已经有现成的 zip 包 (通配符匹配)
string[] existingZips = Directory.GetFiles(baseDir, "OpenTabletDriver*win-x64.zip");
string otdZipPath = existingZips.Length > 0 ? existingZips[0] : Path.Combine(baseDir, otdFileName);

// ==========================================
// 2. 下载环境与软件 (镜像加速 + 原站兜底)
// ==========================================
if (!File.Exists(dotNetPath))
{
    Console.WriteLine("\n[2/4] 下载 .NET 8 桌面运行环境...");
    await DownloadAsync("https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.28/windowsdesktop-runtime-8.0.28-win-x64.exe", dotNetPath);
}

// 核心逻辑判断：如果解压目录已经存在，就不需要下载和解压了
if (Directory.Exists(otdExtractDir))
{
    Console.WriteLine($"\n[3/4] 检测到本地已存在 {Path.GetFileName(otdExtractDir)} 目录，跳过下载与解压。");
}
else
{
    // 目录不存在，检查压缩包在不在
    if (!File.Exists(otdZipPath))
    {
        Console.WriteLine($"\n[3/4] 下载 OTD {versionTag}...");
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
    else
    {
        Console.WriteLine($"\n[3/4] 检测到本地安装包 {Path.GetFileName(otdZipPath)}，跳过下载。");
    }

    // ==========================================
    // 3. 解压与防套娃目录处理
    // ==========================================
    Console.WriteLine("\n-> 正在解压 OTD...");
    ZipFile.ExtractToDirectory(otdZipPath, otdExtractDir);

    Console.WriteLine("-> 解压完成，正在删除安装包...");
    File.Delete(otdZipPath); // 提取完立刻删除压缩包

    Console.WriteLine("-> 正在处理嵌套目录...");
    // 寻找解压出来的 OpenTabletDriver\OpenTabletDriver-0.6.x_win-x64 目录
    string[] nestedDirs = Directory.GetDirectories(otdExtractDir, "OpenTabletDriver*win-x64");
    if (nestedDirs.Length > 0)
    {
        string nestedDir = nestedDirs[0];

        // 1. 把内层所有文件移到外层
        foreach (string file in Directory.GetFiles(nestedDir))
            File.Move(file, Path.Combine(otdExtractDir, Path.GetFileName(file)), true);

        // 2. 把内层所有文件夹移到外层
        foreach (string dir in Directory.GetDirectories(nestedDir))
            Directory.Move(dir, Path.Combine(otdExtractDir, Path.GetFileName(dir)));

        // 3. 删除空掉的内层文件夹
        Directory.Delete(nestedDir);
        Console.WriteLine("-> 目录扁平化处理完毕。");
    }
}

// ==========================================
// 4. 环境安装与便携模式部署
// ==========================================
Console.WriteLine("\n[4/4] 部署环境 (安装 .NET 8 并配置 OTD 便携模式)...");

if (File.Exists(dotNetPath))
{
    Console.WriteLine("-> 正在静默安装 .NET 8 (若弹出权限提示请点击允许)...");
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
    Console.WriteLine("-> 正在执行 convert_to_portable.bat 并自动破解暂停提示...");
    using var batProcess = new Process();
    batProcess.StartInfo.FileName = batPath;
    batProcess.StartInfo.WorkingDirectory = otdExtractDir;

    // 隐藏黑框
    batProcess.StartInfo.CreateNoWindow = true;
    batProcess.StartInfo.UseShellExecute = false;

    // 核心秘籍：接管这个进程的键盘输入
    batProcess.StartInfo.RedirectStandardInput = true;

    batProcess.Start();

    // 提前往它的输入缓冲区发送一个“回车键(\n)”
    // 这样当它执行到最后一句 pause 让你按任意键时，会立刻自动吸收这个回车并退出，绝不卡死！
    batProcess.StandardInput.WriteLine();
    batProcess.StandardInput.Close(); // 关闭输入流，告诉它没按键了

    batProcess.WaitForExit();
    Console.WriteLine("-> 便携模式配置完毕！");
}

// ==========================================
// 底层下载器
// ==========================================
async Task DownloadAsync(string url, string path, int timeoutSeconds = 0)
{
    using var cts = new CancellationTokenSource();
    if (timeoutSeconds > 0) cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

    using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
    response.EnsureSuccessStatusCode();

    long totalBytes = response.Content.Headers.ContentLength ?? -1L;
    using var stream = await response.Content.ReadAsStreamAsync();
    using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);

    byte[] buffer = new byte[8192];
    int read;
    long totalRead = 0;

    while ((read = await stream.ReadAsync(buffer)) > 0)
    {
        await fileStream.WriteAsync(buffer.AsMemory(0, read));
        totalRead += read;
        if (totalBytes > 0)
            Console.Write($"\r进度: {totalRead * 100.0 / totalBytes:F1}% [{totalRead / 1048576.0:F2} MB / {totalBytes / 1048576.0:F2} MB]   ");
    }
    Console.WriteLine();
}