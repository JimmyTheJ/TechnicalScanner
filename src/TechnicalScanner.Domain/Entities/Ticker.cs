namespace TechnicalScanner.Domain.Entities;

public class Ticker
{
    public long Id { get; set; }
    public required string Symbol { get; set; }
    public required string Exchange { get; set; }
    public required string Country { get; set; }
    public string? Currency { get; set; }
    public string? CompanyName { get; set; }
    public string? Sector { get; set; }
    public string? Industry { get; set; }
    public decimal? MarketCap { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsEtf { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<PriceCandle> PriceCandles { get; set; } = [];
    public ICollection<IndicatorValue> IndicatorValues { get; set; } = [];
    public ICollection<SignalResult> SignalResults { get; set; } = [];
    public ICollection<ScanScore> ScanScores { get; set; } = [];
}
