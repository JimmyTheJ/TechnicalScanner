using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Models.Queries;

namespace TechnicalScanner.Application.Services.Queries;

public sealed class ScannerQueryService(
    ITechnicalScannerDbContext dbContext,
    BreadthQueryService breadthQueryService)
{
    public ScannerLatestDto GetLatest()
    {
        var latestScan = dbContext.ScanRuns
            .OrderByDescending(r => r.StartedUtc)
            .FirstOrDefault();

        var latestSignalScan = dbContext.SignalResults
            .OrderByDescending(r => r.ScanDateUtc)
            .Select(r => (DateTimeOffset?)r.ScanDateUtc)
            .FirstOrDefault();

        var activeTickers = dbContext.Tickers.Count(t => t.IsActive);
        var matchedSignals = latestSignalScan is null
            ? 0
            : dbContext.SignalResults.Count(r => r.ScanDateUtc == latestSignalScan && r.IsMatch);

        var breadth = breadthQueryService.GetLatest()
            .Select(b => new BreadthMetricDto
            {
                MetricCode = b.MetricCode,
                TotalValidStocks = b.TotalValidStocks,
                MatchCount = b.MatchCount,
                MatchPct = b.MatchPct
            })
            .ToList();

        return new ScannerLatestDto
        {
            ScanDateUtc = latestSignalScan ?? latestScan?.CompletedUtc ?? latestScan?.StartedUtc,
            ScanRunId = latestScan?.Id,
            ScanStatus = latestScan?.Status,
            ActiveTickers = activeTickers,
            MatchedSignals = matchedSignals,
            Breadth = breadth
        };
    }

    public PagedResult<ScoreRowDto> GetScores(
        string? scoreCode,
        string? country,
        string? exchange,
        decimal? minScore,
        decimal? maxScore,
        string sort,
        int page,
        int pageSize)
    {
        var (normalizedPage, normalizedPageSize, skip) = QueryPaging.Normalize(page, pageSize);
        var latestScanDate = dbContext.ScanScores
            .OrderByDescending(s => s.ScanDateUtc)
            .Select(s => (DateTimeOffset?)s.ScanDateUtc)
            .FirstOrDefault();

        if (latestScanDate is null)
        {
            return new PagedResult<ScoreRowDto>([], normalizedPage, normalizedPageSize, 0);
        }

        var query = dbContext.ScanScores
            .Where(s => s.ScanDateUtc == latestScanDate)
            .Join(dbContext.Tickers, s => s.TickerId, t => t.Id, (s, t) => new { s, t })
            .Where(x => x.t.IsActive);

        if (!string.IsNullOrWhiteSpace(scoreCode))
        {
            query = query.Where(x => x.s.ScoreCode == scoreCode);
        }

        if (!string.IsNullOrWhiteSpace(country))
        {
            query = query.Where(x => x.t.Country == country);
        }

        if (!string.IsNullOrWhiteSpace(exchange))
        {
            query = query.Where(x => x.t.Exchange == exchange);
        }

        if (minScore is not null)
        {
            query = query.Where(x => x.s.Score >= minScore);
        }

        if (maxScore is not null)
        {
            query = query.Where(x => x.s.Score <= maxScore);
        }

        var rows = query
            .Select(x => new ScoreRowDto
            {
                TickerId = x.t.Id,
                Symbol = x.t.Symbol,
                Exchange = x.t.Exchange,
                Country = x.t.Country,
                ScoreCode = x.s.ScoreCode,
                Score = x.s.Score,
                ComponentsJson = x.s.ComponentsJson
            })
            .ToList();

        rows = sort.ToLowerInvariant() switch
        {
            "scoreasc" => rows.OrderBy(r => r.Score).ThenBy(r => r.Symbol).ToList(),
            "symbolasc" => rows.OrderBy(r => r.Symbol).ToList(),
            "symboldesc" => rows.OrderByDescending(r => r.Symbol).ToList(),
            _ => rows.OrderByDescending(r => r.Score).ThenBy(r => r.Symbol).ToList()
        };

        var totalCount = rows.Count;
        var items = rows.Skip(skip).Take(normalizedPageSize).ToList();
        return new PagedResult<ScoreRowDto>(items, normalizedPage, normalizedPageSize, totalCount);
    }
}
