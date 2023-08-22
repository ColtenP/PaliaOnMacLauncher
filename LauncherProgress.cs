namespace PaliaOnMacLauncher;

public class LauncherProgress
{
    public long? CurrentProgress { get; init; }
    public long? MaxProgress { get; init; }
    public string? Message { get; init; }
    public float? ProgressPercentage => CurrentProgress is not null &&
                                        MaxProgress is not null &&
                                        MaxProgress != 0
        ? (CurrentProgress * 1f) * 100f / (MaxProgress * 1f)
        : null;
    public string? FormattedMessage => ProgressPercentage is not null ? $"[{ProgressPercentage:0.0}%] " + Message : Message;
    public bool IsComplete => MaxProgress is not null && CurrentProgress is not null && CurrentProgress == MaxProgress;
}
