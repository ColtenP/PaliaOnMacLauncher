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

    public void Start()
    {
        Console.WriteLine("Starting PaliaOnMacLauncher");
        var patchManifestTask = GetPatchManifest();
        patchManifestTask.Wait();
        var patchManifest = patchManifestTask.Result;
        var launcherConfig = GetLauncherConfig();

        if (!Directory.Exists(_installationPath))
        {
            Console.WriteLine("The specific installation directory does not exist, creating it");
            Directory.CreateDirectory(_installationPath);
            SaveLauncherConfig(launcherConfig);
        }
        
        foreach (var paliaVersion in patchManifest)
        {
            ProcessPaliaVersion(launcherConfig, paliaVersion);
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

    private async Task<List<PaliaVersion>> GetPatchManifest()
    {
        using var client = new HttpClient();
        Console.WriteLine("Attempting to Read Patch Manifest from {0}", _patchManifestUrl);
        var jsonString = await client.GetStringAsync(_patchManifestUrl);
            
        Console.WriteLine("Read Patch Manifest, Deserializing Patch Manifest");
        var manifest = JsonSerializer.Deserialize<Dictionary<string, PaliaVersion>>(jsonString);

        if (manifest is null)
        {
            throw new IOException(
                "Manifest either could not be read, or deserialized. " +
                "Check your internet or maybe the PatchManifest.json format changed.");
        }
        
        foreach (var entry in manifest)
        {
            entry.Value.GameVersion = new(entry.Key);
        }
            
        Console.WriteLine("Deserialized Patch Manifest");

        var versions = manifest.Values.ToList();
        versions.Sort((a, b) => a.GameVersion.CompareTo(b.GameVersion));
        return versions;
    }

    private LauncherConfig GetLauncherConfig()
    {
        Console.WriteLine("Attempting to Read Launcher Config");
        var settingsPath = Path.Combine(_installationPath, "LauncherConfig.json");

        if (File.Exists(settingsPath))
        {
            var config = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(settingsPath));
            Console.WriteLine("Fetched Launcher Config from {0}", settingsPath);

            if (config is null)
            {
                throw new IOException(
                    "LauncherConfig.json either could not be read or deserialized.");
            }
            
            return config;
        }
        else
        {
            Console.WriteLine("Config does not exist, returning default");
            return new LauncherConfig();
        }
    }

    private void SaveLauncherConfig(LauncherConfig config)
    {
        Console.WriteLine("Attempting to Save Launcher Config");
        var settingsPath = Path.Combine(_installationPath, "LauncherConfig.json");
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(config));
    }

    private void ProcessPaliaVersion(LauncherConfig config, PaliaVersion paliaVersion)
    {
        Console.WriteLine("Processing Palia Version of {0}", paliaVersion.GameVersion);

        foreach (var paliaFile in paliaVersion.Files) ProcessPaliaFile(config, paliaVersion, paliaFile);

        if (config.GameVersion.CompareTo(paliaVersion.GameVersion) < 0)
        {
            config.GameVersion = paliaVersion.GameVersion;
            SaveLauncherConfig(config);
        }
    }

    private void ProcessPaliaFile(LauncherConfig config, PaliaVersion paliaVersion, PaliaFile file)
    {
        var fileName = Path.GetFileName(file.URL);
        var fileExtension = fileName.Split(".").Last().ToLower();

        if (fileExtension.Equals("zip")) ProcessZipFile(config, paliaVersion, file);
        else if (fileExtension.Equals("exe")) ProcessExeFile(config, paliaVersion, file);
        else if (fileExtension.Equals("pak")) ProcessPakFile(paliaVersion, file);
        else Console.WriteLine("Unsupported Palia File of type {0} - {1}", fileExtension, file.URL);
    }
    
    private async Task<string> DownloadFile(string url)
    {
        return await DownloadFile(url, Path.Combine(Path.GetTempPath(), Path.GetFileName(url)));
    }
    
    private async Task<string> DownloadFile(string url, string destinationPath)
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
        
        return destinationPath;
    }

    private static EventHandler<float> DownloadProgressCallBack(ProgressBar progressBar)
    {
        return (_, progress) =>
        {
            progressBar.AsProgress<float>().Report(progress);
        };
    }

    private static string GetHashOfFile(string path)
    {
        using var stream = File.OpenRead(path);
        using var bufferedStream = new BufferedStream(stream, 1024 * 32);
        var sha = SHA256.Create();
        var checksum = sha.ComputeHash(bufferedStream);
        return BitConverter.ToString(checksum).Replace("-", String.Empty).ToLower();
    }

    private static void ExtractZip(string zipPath, string basePath)
    {
        try
        {
            using var zipArchive = new ZipFile(zipPath);
            using var progressBar = new ProgressBar(Convert.ToInt32(zipArchive.Count),
                $"Unzipping {Path.GetFileName(zipPath)}");
            foreach (ZipEntry entry in zipArchive)
            {
                try
                {
                    var destinationPath = Path.Combine(basePath, entry.Name);
                    var directoryPath = Path.GetDirectoryName(destinationPath);
                    progressBar.Tick();
                    if (directoryPath is null) continue;
                    if (!Path.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);
                    if (entry.Name.EndsWith("/")) continue;
                    using var streamWriter = File.Create(destinationPath);
                    using var zipStream = zipArchive.GetInputStream(entry);
                    var buffer = new byte[4096];
                    StreamUtils.Copy(zipStream, streamWriter, buffer);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("Corrupted File {0}", entry.Name);
                    Console.Error.WriteLine(e);
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error while opening Zip File: {0}", ex);
        }
    }

    private void ProcessZipFile(LauncherConfig config, PaliaVersion paliaVersion, PaliaFile file)
    {
        // If the installed version is larger than the palia version with this zip, skip processing this file.
        // Zip files do not have hashes, so it's not possible to check, and it should have been installed already.
        if (config.GameVersion.CompareTo(paliaVersion.GameVersion) >= 0) return;
        
        
        if (_localZipFile is not null)
        {
            Console.WriteLine("Using specified local zip file at {0}", _localZipFile);
            Console.WriteLine("Extracting to {0}", _installationPath);
            ExtractZip(_localZipFile, _installationPath);
        }
        else
        {
            Console.WriteLine("Downloading {0}", file.URL);
            var tmpPath = DownloadFile(file.URL);
            
            tmpPath.Wait();
            
            Console.WriteLine("Downloaded {0} to {1}, Extracting to {2}", file.URL, tmpPath.Result, _installationPath);
            ExtractZip(tmpPath.Result, _installationPath);
            
            Console.WriteLine("Cleaning Up {0}", tmpPath.Result);
            File.Delete(tmpPath.Result);
        }
    }

    private void ProcessExeFile(LauncherConfig config, PaliaVersion paliaVersion, PaliaFile file)
    {
        var fileName = Path.GetFileName(file.URL);
        var destinationPath = Path.Combine(_installationPath, "Palia", "Binaries", "Win64", fileName);
        
        Console.WriteLine("Processing {0} for version {1}", fileName, paliaVersion.GameVersion);

        if (!File.Exists(destinationPath) || config.GameVersion.CompareTo(paliaVersion.GameVersion) < 0)
        {
            Console.WriteLine("{0} does not exist locally, downloading.", fileName);
            DownloadFile(file.URL, destinationPath).Wait();
            Console.WriteLine("{0} was downloaded successfully", fileName);
        }

        Console.WriteLine("Hashing {0}", fileName);
        var sha256Hash = GetHashOfFile(destinationPath);

        if (!sha256Hash.Equals(file.Hash) && config.GameVersion.CompareTo(paliaVersion.GameVersion) >= 0)
        {
            Console.WriteLine("The hash provided was {0}, the hash calculated is {1}", file.Hash, sha256Hash);
            Console.WriteLine("The hash for file {0} does not match what is provided in the PatchManifest.json.", destinationPath);
            Console.WriteLine("Press Any Key To Exit.");
            Console.ReadLine();
            Environment.Exit(-1);
        }
        
        Console.WriteLine("The hash of {0} matched with what was provided in PatchManifest.json", fileName);
    }

    private void ProcessPakFile(PaliaVersion paliaVersion, PaliaFile file)
    {
        var fileName = Path.GetFileName(file.URL);
        var destinationPath = Path.Combine(_installationPath, "Palia", "Content", "Paks", fileName);
        
        Console.WriteLine("Processing {0} for version {1}", fileName, paliaVersion.GameVersion);

        if (!File.Exists(destinationPath))
        {
            Console.WriteLine("{0} does not exist locally, downloading.", fileName);
            DownloadFile(file.URL, destinationPath).Wait();
            Console.WriteLine("{0} was downloaded successfully", fileName);
        }

        Console.WriteLine("Hashing {0}", fileName);
        var sha256Hash = GetHashOfFile(destinationPath);

        if (!sha256Hash.Equals(file.Hash))
        {
            Console.Error.WriteLine("The hash provided was {0}, the hash calculated is {1}", file.Hash, sha256Hash);
            Console.Error.WriteLine("The hash for file {0} does not match what is provided in the PatchManifest.json.", destinationPath);
            Console.WriteLine("Press Any Key To Exit.");
            Console.ReadLine();
            Environment.Exit(-1);
        }
        
        Console.WriteLine("The hash of {0} matched with what was provided in PatchManifest.json", fileName);
    }
}