namespace TechnicalScanner.Domain.Entities;

public class BreadthSnapshot
{
    public long Id { get; set; }
    public DateTimeOffset ScanDateUtc { get; set; }
    public required string UniverseName { get; set; }
    public string? Country { get; set; }
    public string? Exchange { get; set; }
    public string? Sector { get; set; }
    public string? Industry { get; set; }
    public required string MetricCode { get; set; }
    public int TotalValidStocks { get; set; }
    public int MatchCount { get; set; }
    public decimal MatchPct { get; set; }
    public decimal? MedianValue { get; set; }
    public decimal? AverageValue { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
