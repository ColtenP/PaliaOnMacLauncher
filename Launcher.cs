using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace PaliaOnMacLauncher;

using System.Text.Json;

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
        try
        {
            Console.WriteLine("Starting PaliaOnMacLauncher");
            LauncherUtils.RewriteLine("Initializing Launcher");
            
            var isDone = false;
            var progress = new Progress<LauncherProgress>(p => {
                if (isDone || p.IsComplete) return;
                LauncherUtils.RewriteLine(p.FormattedMessage);
            });
            
            if (!LauncherUtils.IsRedistributableInstalled())
            {
                Console.WriteLine("Microsoft Visual C++ Redistributable 2022 is not installed. Downloading Installer.");
                var vcRedistFile = new PatchManifest.LauncherFile(
                    "https://aka.ms/vs/17/release/vc_redist.x64.exe",
                    Path.Combine(_installationPath, "vc_redist.x64.exe")
                );
                await LauncherUtils.DownloadFile(vcRedistFile, progress);
                var process = Process.Start(vcRedistFile.LocalPath);
                await process.WaitForExitAsync();
                Process.Start(Environment.ProcessPath!);
                Process.GetCurrentProcess().Kill();
            }
            
            var launcherFiles = await GetLauncherFiles();
            if (launcherFiles is null) throw new Exception("Could not fetch PatchManifest");
            if (!Directory.Exists(_installationPath)) Directory.CreateDirectory(_installationPath);
            
            foreach (var launcherFile in launcherFiles)
            {
                LauncherUtils.RewriteLine($"Processing {launcherFile.FileName}");
                await ProcessLauncherFile(launcherFile, progress);
                LauncherUtils.RewriteLine($"Processed {launcherFile.FileName}\n");
            }

            isDone = true;

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
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            Console.ReadLine();
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
    
    private async Task ProcessLauncherFile(PatchManifest.LauncherFile file, IProgress<LauncherProgress>? progress)
    {
        if (file.LocalPath.EndsWith(".zip")) await ProcessZipFile(file, progress);
        else await ProcessPaliaFile(file, progress);
    }

    private async Task ProcessZipFile(PatchManifest.LauncherFile file, IProgress<LauncherProgress>? progress)
    {
        if (DoesInstallationExist) return;

        if (_localZipFile is null)
        {
            await LauncherUtils.DownloadFile(file, progress);
        }
        else
        {
            file.LocalPath = _localZipFile;
        }
        
        await LauncherUtils.ExtractZip(file.LocalPath, _installationPath, progress);
        
        if (_localZipFile is null) File.Delete(file.LocalPath);
    }

    private static async Task ProcessPaliaFile(PatchManifest.LauncherFile file, IProgress<LauncherProgress>? progress)
    {
        if (!Path.Exists(file.LocalPath) || !CalculateFileHash(file, progress).Equals(file.Hash))
        {
            await LauncherUtils.DownloadFile(file, progress);
        }

        if (!CalculateFileHash(file, progress).Equals(file.Hash))
        {
            throw new Exception($"File hash does not match for {file.FileName}");
        }
    }

    private static string CalculateFileHash(PatchManifest.LauncherFile file, IProgress<LauncherProgress>? progress)
    {
        progress?.Report(new LauncherProgress
        {
            Message = $"Hashing {file.FileName}"
        });
        using var stream = File.OpenRead(file.LocalPath);
        using var sha256 = SHA256.Create();
        var checksum = sha256.ComputeHash(stream);
        var hash = BitConverter.ToString(checksum).Replace("-", string.Empty).ToLower();
        progress?.Report(new LauncherProgress
        {
            Message = $"Hashed {file.FileName}"
        });

        return hash;
    }
}
