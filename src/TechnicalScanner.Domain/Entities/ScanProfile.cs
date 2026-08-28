namespace TechnicalScanner.Domain.Entities;

public class ScanProfile
{
    public long Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string UniverseJson { get; set; }
    public required string IndicatorsJson { get; set; }
    public required string RuleJson { get; set; }
    public required string ScoreWeightsJson { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
