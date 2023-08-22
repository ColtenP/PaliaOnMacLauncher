using System.Text.Json.Serialization;

namespace PaliaOnMacLauncher;

public class PatchManifest: Dictionary<string, PatchManifest.PaliaVersion>
{
    public List<LauncherFile> GetLauncherFiles(string installationPath) =>
    (
        from entry in this
        from file in entry.Value.Files
        select new LauncherFile(entry.Key, file.Url, file.Hash, installationPath)
    ).ToList();
        
    
    public class PaliaVersion
    {
        public bool BaseLineVer { get; set; } = false;
        public PaliaFile[] Files { get; set; } = Array.Empty<PaliaFile>();
    }

    public class PaliaFile
    {
        [JsonPropertyName("URL")]
        public string Url { get; set; }
        public string Hash { get; set; }
    }

    public class LauncherFile
    {
        public string GameVersion { get; set; }
        public string Url { get; set; }
        public string Hash { get; set; }
        public string LocalPath { get; set; }
        public string FileName => Path.GetFileName(LocalPath);

        public LauncherFile(string gameVersion, string url, string hash, string installationPath)
        {
            GameVersion = gameVersion;
            Url = url;
            Hash = hash;
            LocalPath = url.Split(".").Last() switch
            {
                "zip" => Path.Combine(installationPath, Path.GetFileName(url)),
                "pak" => Path.Combine(installationPath, "Palia", "Content", "Paks", Path.GetFileName(url)),
                "exe" => Path.Combine(installationPath, "Palia", "Binaries", "Win64", Path.GetFileName(url)),
                _ => throw new ArgumentException("Invalid file type provided")
            };
        }
    }
}