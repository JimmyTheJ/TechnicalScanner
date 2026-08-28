using TechnicalScanner.Application.Models.Profiles;

namespace TechnicalScanner.Application.Models.Queries;

public sealed class ScreenerQuery
{
    public string? Country { get; set; }
    public string? Exchange { get; set; }
    public string? Sector { get; set; }
    public string? Industry { get; set; }
    public decimal? MinMarketCap { get; set; }
    public decimal? MaxMarketCap { get; set; }
    public string? Signal { get; set; }
    public string? BmsbStatus { get; set; }
    public decimal? MinDailyRsi { get; set; }
    public decimal? MaxDailyRsi { get; set; }
    public decimal? MinWeeklyRsi { get; set; }
    public decimal? MaxWeeklyRsi { get; set; }
    public decimal? MinCompositeScore { get; set; }
    public decimal? MaxCompositeScore { get; set; }
    public decimal? MinBollingerPercentB { get; set; }
    public decimal? MaxBollingerPercentB { get; set; }
    public string? Profile { get; set; }
    public bool ProfileMatchOnly { get; set; }
    public string? Search { get; set; }
    public string Sort { get; set; } = "compositeScoreDesc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = QueryPaging.DefaultPageSize;
}

public sealed class ScreenerRowDto
{
    public long TickerId { get; init; }
    public required string Symbol { get; init; }
    public string? CompanyName { get; init; }
    public required string Exchange { get; init; }
    public required string Country { get; init; }
    public string? Sector { get; init; }
    public string? Industry { get; init; }
    public decimal? MarketCap { get; init; }
    public decimal? Close { get; init; }
    public string? BmsbStatus { get; init; }
    public decimal? BmsbDistancePct { get; init; }
    public decimal? DailyRsi { get; init; }
    public decimal? WeeklyRsi { get; init; }
    public decimal? MonthlyRsi { get; init; }
    public decimal? Sma200DDistancePct { get; init; }
    public decimal? Sma50WDistancePct { get; init; }
    public decimal? Sma100WDistancePct { get; init; }
    public decimal? Sma200WDistancePct { get; init; }
    public decimal? DailyBollingerPercentB { get; init; }
    public decimal? WeeklyBollingerPercentB { get; init; }
    public decimal? CompositeScore { get; init; }
    public bool? ProfileMatch { get; init; }
    public string? ProfileCode { get; init; }
    public IReadOnlyList<ScreenerMetricDto> Metrics { get; init; } = [];
    public IReadOnlyList<string> MatchedSignals { get; init; } = [];
}
