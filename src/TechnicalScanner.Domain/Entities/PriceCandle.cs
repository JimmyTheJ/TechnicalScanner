namespace TechnicalScanner.Domain.Entities;

public class PriceCandle
{
    public long Id { get; set; }
    public long TickerId { get; set; }
    public required string Timeframe { get; set; }
    public DateOnly CandleDate { get; set; }
    public decimal? Open { get; set; }
    public decimal? High { get; set; }
    public decimal? Low { get; set; }
    public decimal Close { get; set; }
    public decimal? AdjustedClose { get; set; }
    public long? Volume { get; set; }
    public required string Source { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public Ticker Ticker { get; set; } = null!;
}
