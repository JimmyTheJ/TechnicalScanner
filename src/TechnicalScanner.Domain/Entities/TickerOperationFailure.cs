namespace TechnicalScanner.Domain.Entities;

public class TickerOperationFailure
{
    public long Id { get; set; }
    public long? ScanRunId { get; set; }
    public long? TickerId { get; set; }
    public required string Symbol { get; set; }
    public required string Operation { get; set; }
    public required string ErrorMessage { get; set; }
    public DateTimeOffset OccurredUtc { get; set; } = DateTimeOffset.UtcNow;

    public ScanRun? ScanRun { get; set; }
    public Ticker? Ticker { get; set; }
}
