using System.Runtime.InteropServices;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Win32;

namespace PaliaOnMacLauncher;

public static class LauncherUtils
{
    public static void RewriteLine(string? value, params object[] param)
    {
        Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r" + value, param);
    }

    public static bool IsRedistributableInstalled()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return true;

        using var registryKey = Registry.LocalMachine
            .OpenSubKey(@"Software\Wow6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\X64");
        var value = int.TryParse(registryKey?.GetValue("Bld")?.ToString(), out var bld);
        
        return value && bld >= 32532;
    }
    
    public static async Task DownloadFile(PatchManifest.LauncherFile file, IProgress<LauncherProgress>? progress)
    {
        using var client = new HttpClient();
        
        var progressWrapper = new Progress<LauncherProgress>(p =>
        {
            if (p.IsComplete) return;
            progress?.Report(new LauncherProgress
            {
                CurrentProgress = p.CurrentProgress,
                MaxProgress = p.MaxProgress,
                Message = $"Downloading {file.FileName}"
            });
        });
        
        await using var fs = new FileStream(file.LocalPath, FileMode.Create);
        await client.DownloadDataAsync(file.Url, fs, progressWrapper);
    }
    
    public static async Task ExtractZip(string zipPath, string basePath, IProgress<LauncherProgress>? progress)
    {
        try
        {
            using var zipArchive = new ZipFile(zipPath);
            foreach (ZipEntry entry in zipArchive)
            {
                try
                {
                    progress?.Report(new LauncherProgress
                    {
                        CurrentProgress = entry.ZipFileIndex,
                        MaxProgress = zipArchive.Count,
                        Message = $"Unzipping {entry.Name}"
                    });
                    var destinationPath = Path.Combine(basePath, entry.Name);
                    var directoryPath = Path.GetDirectoryName(destinationPath);
                    if (directoryPath is null) continue;
                    if (!Path.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);
                    if (entry.Name.EndsWith("/")) continue;
                    await using var streamWriter = File.Create(destinationPath);
                    await using var zipStream = zipArchive.GetInputStream(entry);
                    var buffer = new byte[4096];
                    var progressWrapper = new Progress<long>(p =>
                    {
                        progress?.Report(new LauncherProgress
                        {
                            CurrentProgress = p,
                            MaxProgress = entry.Size,
                            Message = $"Unzipping {entry.Name}"
                        });
                    });
                    await zipStream.CopyToAsync(streamWriter, 4096, progressWrapper);
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
}