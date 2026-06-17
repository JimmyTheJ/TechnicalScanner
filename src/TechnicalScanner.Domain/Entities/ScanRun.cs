namespace TechnicalScanner.Domain.Entities;

public class ScanRun
{
    public long Id { get; set; }
    public DateTimeOffset StartedUtc { get; set; }
    public DateTimeOffset? CompletedUtc { get; set; }
    public required string Status { get; set; }
    public int TickersProcessed { get; set; }
    public int TickersFailed { get; set; }
    public string? SummaryJson { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<TickerOperationFailure> Failures { get; set; } = [];
}
