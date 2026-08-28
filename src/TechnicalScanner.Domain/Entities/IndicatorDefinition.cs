namespace TechnicalScanner.Domain.Entities;

public class IndicatorDefinition
{
    public long Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string IndicatorType { get; set; }
    public required string Timeframe { get; set; }
    public required string ParametersJson { get; set; }
    public bool IsActive { get; set; } = true;
    public bool NeedsRecalculation { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<IndicatorValue> IndicatorValues { get; set; } = [];
    public ICollection<SignalDefinition> SignalDefinitions { get; set; } = [];
}
