namespace TechnicalScanner.Domain.Entities;

public class SignalResult
{
    public long Id { get; set; }
    public DateTimeOffset ScanDateUtc { get; set; }
    public long TickerId { get; set; }
    public long SignalDefinitionId { get; set; }
    public bool IsMatch { get; set; }
    public decimal? Score { get; set; }
    public string? ValuesJson { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public Ticker Ticker { get; set; } = null!;
    public SignalDefinition SignalDefinition { get; set; } = null!;
}
