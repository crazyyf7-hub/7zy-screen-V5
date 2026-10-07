namespace SevenzyX.Models;

public sealed class ScanCheckResult
{
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string Detail { get; init; }
    public double Weight { get; init; }
    public double Score { get; init; }
}

public sealed class SystemScanResult
{
    public int Percent { get; init; }
    public required IReadOnlyList<ScanCheckResult> Checks { get; init; }
    public DateTime ScannedAt { get; init; }
}
