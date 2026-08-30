namespace TechnicalScanner.Application.Models.Queries;

public sealed class ScannerLatestDto
{
    public DateTimeOffset? ScanDateUtc { get; init; }
    public long? ScanRunId { get; init; }
    public string? ScanStatus { get; init; }
    public int ActiveTickers { get; init; }
    public int MatchedSignals { get; init; }
    public IReadOnlyList<BreadthMetricDto> Breadth { get; init; } = [];
}

public sealed class BreadthMetricDto
{
    public required string MetricCode { get; init; }
    public int TotalValidStocks { get; init; }
    public int MatchCount { get; init; }
    public decimal MatchPct { get; init; }
}

public sealed class SignalMatchDto
{
    public long TickerId { get; init; }
    public required string Symbol { get; init; }
    public required string Exchange { get; init; }
    public required string Country { get; init; }
    public required string SignalCode { get; init; }
    public required string SignalName { get; init; }
    public decimal? Score { get; init; }
}

public sealed class ScoreRowDto
{
    public long TickerId { get; init; }
    public required string Symbol { get; init; }
    public required string Exchange { get; init; }
    public required string Country { get; init; }
    public required string ScoreCode { get; init; }
    public decimal Score { get; init; }
    public string? ComponentsJson { get; init; }
}

public sealed class IndicatorHistoryPointDto
{
    public DateOnly CandleDate { get; init; }
    public decimal? PrimaryValue { get; init; }
    public decimal? SecondaryValue { get; init; }
    public decimal? TertiaryValue { get; init; }
    public required string ValuesJson { get; init; }
}

public sealed class CreateIndicatorDefinitionRequest
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string IndicatorType { get; set; }
    public required string Timeframe { get; set; }
    public required string ParametersJson { get; set; }
}

public sealed class CreateSignalDefinitionRequest
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? RuleJson { get; set; }
    public string? Severity { get; set; }
    public string? Direction { get; set; }
}

public sealed class PriceCandlePointDto
{
    public DateOnly CandleDate { get; init; }
    public decimal? Open { get; init; }
    public decimal? High { get; init; }
    public decimal? Low { get; init; }
    public decimal Close { get; init; }
    public long? Volume { get; init; }
}

public sealed class TickerSignalHistoryDto
{
    public DateTimeOffset ScanDateUtc { get; init; }
    public required string SignalCode { get; init; }
    public required string SignalName { get; init; }
    public bool IsMatch { get; init; }
}
