namespace TechnicalScanner.Application.Models.Queries;

public sealed class TickerListQuery
{
    public string? Country { get; set; }
    public string? Exchange { get; set; }
    public string? Sector { get; set; }
    public string? Industry { get; set; }
    public decimal? MinMarketCap { get; set; }
    public decimal? MaxMarketCap { get; set; }
    public bool? IsEtf { get; set; }
    public string? Search { get; set; }
    public string Sort { get; set; } = "symbolAsc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = QueryPaging.DefaultPageSize;
}

public sealed class TickerListItemDto
{
    public long Id { get; init; }
    public required string Symbol { get; init; }
    public required string Exchange { get; init; }
    public required string Country { get; init; }
    public string? CompanyName { get; init; }
    public string? Sector { get; init; }
    public string? Industry { get; init; }
    public decimal? MarketCap { get; init; }
    public bool IsEtf { get; init; }
}
