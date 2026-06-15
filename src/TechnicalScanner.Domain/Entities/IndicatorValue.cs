namespace TechnicalScanner.Domain.Entities;

public class IndicatorValue
{
    public long Id { get; set; }
    public long TickerId { get; set; }
    public long IndicatorDefinitionId { get; set; }
    public DateOnly CandleDate { get; set; }
    public required string Timeframe { get; set; }
    public decimal? PrimaryValue { get; set; }
    public decimal? SecondaryValue { get; set; }
    public decimal? TertiaryValue { get; set; }
    public required string ValuesJson { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public Ticker Ticker { get; set; } = null!;
    public IndicatorDefinition IndicatorDefinition { get; set; } = null!;
}
