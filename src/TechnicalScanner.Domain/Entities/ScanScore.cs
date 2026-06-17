namespace TechnicalScanner.Domain.Entities;

public class ScanScore
{
    public long Id { get; set; }
    public long? ScanRunId { get; set; }
    public DateTimeOffset ScanDateUtc { get; set; }
    public long TickerId { get; set; }
    public required string ScoreCode { get; set; }
    public decimal Score { get; set; }
    public required string ComponentsJson { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public Ticker Ticker { get; set; } = null!;
    public ScanRun? ScanRun { get; set; }
}
