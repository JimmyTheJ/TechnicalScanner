using System.Text.Json;
using System.Text.Json.Serialization;

namespace TechnicalScanner.Application.Models.Profiles;

public static class ProfileJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static T Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json, Options)
           ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name}.");

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}

public sealed class ProfileUniverse
{
    public string? Country { get; set; }
    public bool IncludeEtfs { get; set; } = true;
    public decimal? MinDollarVolume { get; set; }
    public int? MinHistoryDays { get; set; }
}

public sealed class ProfileIndicatorSpec
{
    public required string Code { get; set; }
    public string? Name { get; set; }
    public string? IndicatorType { get; set; }
    public string? Timeframe { get; set; }
    public JsonElement? Parameters { get; set; }
}

public sealed class ScoreWeightSpec
{
    public required string Label { get; set; }
    public required string IndicatorCode { get; set; }
    public required string Field { get; set; }
    public required string Operator { get; set; }
    public JsonElement Value { get; set; }
    public decimal Weight { get; set; }
}

public sealed class ComparisonCondition
{
    public required string IndicatorCode { get; set; }
    public required string Field { get; set; }
    public required string Operator { get; set; }
    public JsonElement Value { get; set; }
}

public sealed class ScanProfileDto
{
    public long Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public ProfileUniverse Universe { get; init; } = new();
    public IReadOnlyList<ProfileIndicatorSpec> Indicators { get; init; } = [];
    public required string RuleJson { get; init; }
    public IReadOnlyList<ScoreWeightSpec> ScoreWeights { get; init; } = [];
    public bool IsActive { get; init; }
    public IReadOnlyList<string> MissingCalculationCodes { get; init; } = [];
    public DateTimeOffset UpdatedUtc { get; init; }
}

public sealed class UpsertScanProfileRequest
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public ProfileUniverse? Universe { get; set; }
    public IReadOnlyList<ProfileIndicatorSpec>? Indicators { get; set; }
    public string? RuleJson { get; set; }
    public IReadOnlyList<ScoreWeightSpec>? ScoreWeights { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ScreenerMetricDto
{
    public required string Code { get; init; }
    public decimal? PrimaryValue { get; init; }
    public decimal? SecondaryValue { get; init; }
}

public sealed class ProfileBacktestHitDto
{
    public DateOnly Date { get; init; }
    public decimal Close { get; init; }
    public decimal? ForwardReturnPct { get; init; }
    public decimal? Score { get; init; }
}

public sealed class ProfileBacktestResultDto
{
    public required string ProfileCode { get; init; }
    public required string Symbol { get; init; }
    public int MatchDays { get; init; }
    public int EntryCount { get; init; }
    public int ForwardDays { get; init; }
    public IReadOnlyList<ProfileBacktestHitDto> Entries { get; init; } = [];
}

public sealed class ProfileUniverseBacktestHitDto
{
    public required string Symbol { get; init; }
    public DateOnly EntryDate { get; init; }
    public decimal Close { get; init; }
    public decimal? ForwardReturnPct { get; init; }
    public decimal? Score { get; init; }
}

public sealed class ProfileUniverseBacktestDto
{
    public long RunId { get; init; }
    public required string ProfileCode { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset StartedUtc { get; init; }
    public DateTimeOffset? CompletedUtc { get; init; }
    public int ForwardDays { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public int TickerCount { get; init; }
    public int TickersWithEntries { get; init; }
    public int EntryCount { get; init; }
    public int MatchDays { get; init; }
    public decimal? MedianForwardReturnPct { get; init; }
    public decimal? MeanForwardReturnPct { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<ProfileUniverseBacktestHitDto> SampleEntries { get; init; } = [];
}

public sealed class ProfileScoreComponentDto
{
    public required string Label { get; init; }
    public required string IndicatorCode { get; init; }
    public required string Field { get; init; }
    public required string Operator { get; init; }
    public bool Matched { get; init; }
    public decimal Weight { get; init; }
}

public sealed class ProfileTickerSnapshotDto
{
    public required string ProfileCode { get; init; }
    public required string Symbol { get; init; }
    public bool ProfileMatch { get; init; }
    public decimal? Score { get; init; }
    public IReadOnlyList<ProfileScoreComponentDto> Components { get; init; } = [];
    public IReadOnlyList<ScreenerMetricDto> Metrics { get; init; } = [];
}

public sealed class IndicatorCalculationResultDto
{
    public int Calculated { get; init; }
    public IReadOnlyList<string> Codes { get; init; } = [];
}
