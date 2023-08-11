namespace PaliaOnMacLauncher;

public class PaliaVersion
{
    public Version GameVersion { get; set; } = new("0.0.0");
    public bool BaseLineVer { get; set; } = false;
    public PaliaFile[] Files { get; set; } = Array.Empty<PaliaFile>();
}