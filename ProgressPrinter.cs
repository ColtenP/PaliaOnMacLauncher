namespace PaliaOnMacLauncher;

public class ProgressPrinter
{
    private bool IsDeterminate { get; set; } = false;
    private int IndeterminateProgress { get; set; } = 0;
    public ProgressReport? OverallProgress { get; set; }
    public ProgressReport? TaskProgress { get; set; }

    public void ClearLine()
    {
        Console.WriteLine("\r" + new String(' ', Console.BufferWidth) + "\r");
    }

    public class ProgressReport
    {
        public string? Message { get; set; }
        public int? Current { get; set; }
        public int? Max { get; set; }
        public int? Eta { get; set; }
    }
}