using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using ShellProgressBar;

namespace PaliaOnMacLauncher;

using System.Text.Json;
using System.Linq;

public class Launcher
{
    private readonly string _installationPath;
    private readonly string _patchManifestUrl;
    private readonly string? _localZipFile;

    public Launcher(string installationPath, string patchManifestUrl, string? localZipFile)
    {
        _installationPath = installationPath;
        _patchManifestUrl = patchManifestUrl;
        _localZipFile = localZipFile;
    }

    public async Task Start()
    {
        Console.WriteLine("Starting PaliaOnMacLauncher");
        var launcherFiles = await GetLauncherFiles();

        if (launcherFiles is null)
        {
            await Console.Error.WriteLineAsync("Could not fetch PatchManifest");
            Console.ReadLine();
            Environment.Exit(-1);
        }

        if (!Directory.Exists(_installationPath)) Directory.CreateDirectory(_installationPath);

        foreach (var launcherFile in launcherFiles)
        {
            await ProcessLauncherFile(launcherFile);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(Path.Combine(_installationPath, "Palia.exe"));
            Process.GetCurrentProcess().Kill();
        }
        else
        {
            Console.WriteLine("To play the game, please run Windows, or run this launcher under Wine");
        }
    }

    private bool DoesInstallationExist => File.Exists(Path.Combine(_installationPath, "Palia.exe"));

    private async Task<List<PatchManifest.LauncherFile>?> GetLauncherFiles()
    {
        using var client = new HttpClient();
        var json = await client.GetStringAsync(_patchManifestUrl);
        var manifest = JsonSerializer.Deserialize<PatchManifest>(json);
        var files = manifest?.GetLauncherFiles(_installationPath);

        return files;
    }
    
    private async Task ProcessLauncherFile(PatchManifest.LauncherFile file)
    {
        if (file.LocalPath.EndsWith(".zip")) await ProcessZipFile(file);
        else await ProcessPaliaFile(file);
    }

    private async Task ProcessZipFile(PatchManifest.LauncherFile file)
    {
        if (DoesInstallationExist) return;

        if (_localZipFile is not null) file.LocalPath = _localZipFile;
        if (!Path.Exists(file.LocalPath))
        {
            Console.WriteLine("Downloading {0}", file.FileName);
            await DownloadFile(file.Url, file.LocalPath);
            Console.WriteLine("Downloaded {0}", file.FileName);
        }
        
        ExtractZip(file.LocalPath, _installationPath);
        
        if (_localZipFile is null) File.Delete(file.LocalPath);
    }

    private async Task ProcessPaliaFile(PatchManifest.LauncherFile file)
    {
        if (!Path.Exists(file.LocalPath) || !CalculateFileHash(file.LocalPath).Equals(file.Hash))
        {
            Console.WriteLine("Downloading {0}", file.FileName);
            await DownloadFile(file.Url, file.LocalPath);
            Console.WriteLine("Downloaded {0}", file.FileName);
        }

        if (!CalculateFileHash(file.LocalPath).Equals(file.Hash))
        {
            Console.Error.WriteLine("File Hashes Do Not Match for {0}", file.FileName);
            Console.ReadLine();
            Environment.Exit(-1);
        }
    }
    
    private static async Task DownloadFile(string url, string destinationPath)
    {
        using var client = new HttpClient();
        
        var progress = new Progress<float>();
        var fileName = Path.GetFileName(destinationPath);
        var progressBarOptions = new ProgressBarOptions
        {
            CollapseWhenFinished = true,
            ProgressBarOnBottom = false
        };
        var progressBar = new ProgressBar(10000, fileName, progressBarOptions);
        
        progress.ProgressChanged += DownloadProgressCallBack(progressBar);
        
        await using var fs = new FileStream(destinationPath, FileMode.Create);
        await client.DownloadDataAsync(url, fs, progress);
        progressBar.Dispose();
    }

    private static EventHandler<float> DownloadProgressCallBack(ProgressBar progressBar)
    {
        return (_, progress) =>
        {
            progressBar.AsProgress<float>().Report(progress);
        };
    }

    private static string CalculateFileHash(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha256 = SHA256.Create();
        var checksum = sha256.ComputeHash(stream);
        return BitConverter.ToString(checksum).Replace("-", String.Empty).ToLower();
    }
    
}
