namespace TechnicalScanner.Domain.Entities;

public class SignalDefinition
{
    public long Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public long? IndicatorDefinitionId { get; set; }
    public required string RuleJson { get; set; }
    public string? Severity { get; set; }
    public string? Direction { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public IndicatorDefinition? IndicatorDefinition { get; set; }
    public ICollection<SignalResult> SignalResults { get; set; } = [];
}
