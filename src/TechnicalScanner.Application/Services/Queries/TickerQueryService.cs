using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Models.Queries;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Services.Queries;

public sealed class TickerQueryService(ITechnicalScannerDbContext dbContext)
{
    public Task<PagedResult<TickerListItemDto>> GetTickersAsync(
        TickerListQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (page, pageSize, skip) = QueryPaging.Normalize(query.Page, query.PageSize);
        var tickers = ApplyFilters(dbContext.Tickers.Where(t => t.IsActive), query);

        var totalCount = tickers.Count();
        var items = ApplySort(tickers, query.Sort)
            .Skip(skip)
            .Take(pageSize)
            .Select(t => new TickerListItemDto
            {
                Id = t.Id,
                Symbol = t.Symbol,
                Exchange = t.Exchange,
                Country = t.Country,
                CompanyName = t.CompanyName,
                Sector = t.Sector,
                Industry = t.Industry,
                MarketCap = t.MarketCap,
                IsEtf = t.IsEtf
            })
            .ToList();

        return Task.FromResult(new PagedResult<TickerListItemDto>(items, page, pageSize, totalCount));
    }

    public TickerListItemDto? GetTickerBySymbol(string symbol)
    {
        return dbContext.Tickers
            .Where(t => t.IsActive && t.Symbol == symbol)
            .Select(t => new TickerListItemDto
            {
                Id = t.Id,
                Symbol = t.Symbol,
                Exchange = t.Exchange,
                Country = t.Country,
                CompanyName = t.CompanyName,
                Sector = t.Sector,
                Industry = t.Industry,
                MarketCap = t.MarketCap,
                IsEtf = t.IsEtf
            })
            .FirstOrDefault();
    }

    private static IQueryable<Ticker> ApplyFilters(IQueryable<Ticker> query, TickerListQuery filters)
    {
        if (!string.IsNullOrWhiteSpace(filters.Country))
        {
            query = query.Where(t => t.Country == filters.Country);
        }

        if (!string.IsNullOrWhiteSpace(filters.Exchange))
        {
            query = query.Where(t => t.Exchange == filters.Exchange);
        }

        if (!string.IsNullOrWhiteSpace(filters.Sector))
        {
            query = query.Where(t => t.Sector == filters.Sector);
        }

        if (!string.IsNullOrWhiteSpace(filters.Industry))
        {
            query = query.Where(t => t.Industry == filters.Industry);
        }

        if (filters.MinMarketCap is not null)
        {
            query = query.Where(t => t.MarketCap >= filters.MinMarketCap);
        }

        if (filters.MaxMarketCap is not null)
        {
            query = query.Where(t => t.MarketCap <= filters.MaxMarketCap);
        }

        if (filters.IsEtf is not null)
        {
            query = query.Where(t => t.IsEtf == filters.IsEtf);
        }

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var search = filters.Search.Trim();
            query = query.Where(t =>
                t.Symbol.Contains(search) ||
                (t.CompanyName != null && t.CompanyName.Contains(search)));
        }

        return query;
    }

    private static IQueryable<Ticker> ApplySort(IQueryable<Ticker> query, string sort)
        => sort.ToLowerInvariant() switch
        {
            "symboldesc" => query.OrderByDescending(t => t.Symbol),
            "marketcapasc" => query.OrderBy(t => t.MarketCap),
            "marketcapdesc" => query.OrderByDescending(t => t.MarketCap),
            "companynameasc" => query.OrderBy(t => t.CompanyName),
            _ => query.OrderBy(t => t.Symbol)
        };
}
