namespace PaliaOnMacLauncher;

public static class LauncherUtils
{
    private static (Task, float => void) ExtractZip(string zipPath, string basePath)
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
}