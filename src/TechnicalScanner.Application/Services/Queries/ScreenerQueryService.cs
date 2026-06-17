using System.Text.Json;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Models.Queries;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Services.Queries;

public sealed class ScreenerQueryService(ITechnicalScannerDbContext dbContext)
{
    public PagedResult<ScreenerRowDto> GetScreener(ScreenerQuery query, CancellationToken cancellationToken)
    {
        var (page, pageSize, skip) = QueryPaging.Normalize(query.Page, query.PageSize);
        var latestScanDate = dbContext.SignalResults
            .OrderByDescending(r => r.ScanDateUtc)
            .Select(r => (DateTimeOffset?)r.ScanDateUtc)
            .FirstOrDefault();

        var tickers = ApplyTickerFilters(dbContext.Tickers.Where(t => t.IsActive), query).ToList();
        if (tickers.Count == 0)
        {
            return new PagedResult<ScreenerRowDto>([], page, pageSize, 0);
        }

        var tickerIds = tickers.Select(t => t.Id).ToHashSet();
        var indicatorLookup = BuildLatestIndicatorLookup(tickerIds);
        var matchedSignals = BuildMatchedSignals(latestScanDate, tickerIds);
        var scores = BuildLatestScores(tickerIds);

        var rows = new List<ScreenerRowDto>();
        foreach (var ticker in tickers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var indicators = indicatorLookup.GetValueOrDefault(ticker.Id) ?? [];
            var row = BuildRow(ticker, indicators, matchedSignals.GetValueOrDefault(ticker.Id), scores.GetValueOrDefault(ticker.Id));
            if (!PassesFilters(row, query, matchedSignals.GetValueOrDefault(ticker.Id)))
            {
                continue;
            }

            rows.Add(row);
        }

        rows = ApplySort(rows, query.Sort);
        var totalCount = rows.Count;
        var pageItems = rows.Skip(skip).Take(pageSize).ToList();
        return new PagedResult<ScreenerRowDto>(pageItems, page, pageSize, totalCount);
    }

    private static IQueryable<Ticker> ApplyTickerFilters(IQueryable<Ticker> query, ScreenerQuery filters)
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

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var search = filters.Search.Trim();
            query = query.Where(t =>
                t.Symbol.Contains(search) ||
                (t.CompanyName != null && t.CompanyName.Contains(search)));
        }

        return query;
    }

    private Dictionary<long, Dictionary<string, JsonElement>> BuildLatestIndicatorLookup(HashSet<long> tickerIds)
    {
        return dbContext.IndicatorValues
            .Where(v => tickerIds.Contains(v.TickerId))
            .Join(dbContext.IndicatorDefinitions, v => v.IndicatorDefinitionId, d => d.Id, (v, d) => new { v, d.Code })
            .AsEnumerable()
            .GroupBy(x => (x.v.TickerId, x.Code))
            .Select(g => g.OrderByDescending(x => x.v.CandleDate).First())
            .GroupBy(x => x.v.TickerId)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(
                    x => x.Code,
                    x => JsonSerializer.Deserialize<JsonElement>(x.v.ValuesJson)));
    }

    private Dictionary<long, List<string>> BuildMatchedSignals(DateTimeOffset? latestScanDate, HashSet<long> tickerIds)
    {
        if (latestScanDate is null)
        {
            return [];
        }

        return dbContext.SignalResults
            .Where(r => r.ScanDateUtc == latestScanDate && r.IsMatch && tickerIds.Contains(r.TickerId))
            .Join(dbContext.SignalDefinitions, r => r.SignalDefinitionId, s => s.Id, (r, s) => new { r.TickerId, s.Code })
            .AsEnumerable()
            .GroupBy(x => x.TickerId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Code).OrderBy(x => x).ToList());
    }

    private Dictionary<long, (decimal Score, string ComponentsJson)> BuildLatestScores(HashSet<long> tickerIds)
    {
        return dbContext.ScanScores
            .Where(s => tickerIds.Contains(s.TickerId) && s.ScoreCode == "DEEP_PULLBACK")
            .AsEnumerable()
            .GroupBy(s => s.TickerId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var latest = g.OrderByDescending(x => x.ScanDateUtc).First();
                    return (latest.Score, latest.ComponentsJson);
                });
    }

    private static ScreenerRowDto BuildRow(
        Ticker ticker,
        Dictionary<string, JsonElement> indicators,
        List<string>? matchedSignals,
        (decimal Score, string ComponentsJson)? score)
    {
        return new ScreenerRowDto
        {
            TickerId = ticker.Id,
            Symbol = ticker.Symbol,
            CompanyName = ticker.CompanyName,
            Exchange = ticker.Exchange,
            Country = ticker.Country,
            Sector = ticker.Sector,
            Industry = ticker.Industry,
            MarketCap = ticker.MarketCap,
            Close = ReadDecimal(indicators, "BMSB_W", "close")
                ?? ReadDecimal(indicators, "RSI_14_D", "close")
                ?? ReadDecimal(indicators, "SMA_200_D", "close"),
            BmsbStatus = ReadString(indicators, "BMSB_W", "status"),
            BmsbDistancePct = ReadDecimal(indicators, "BMSB_W", "distanceFromBandMidpointPct"),
            DailyRsi = ReadDecimal(indicators, "RSI_14_D", "rsi"),
            WeeklyRsi = ReadDecimal(indicators, "RSI_14_W", "rsi"),
            MonthlyRsi = ReadDecimal(indicators, "RSI_14_M", "rsi"),
            Sma200DDistancePct = ReadDecimal(indicators, "SMA_200_D", "distanceFromSmaPct"),
            Sma50WDistancePct = ReadDecimal(indicators, "SMA_50_W", "distanceFromSmaPct"),
            Sma100WDistancePct = ReadDecimal(indicators, "SMA_100_W", "distanceFromSmaPct"),
            Sma200WDistancePct = ReadDecimal(indicators, "SMA_200_W", "distanceFromSmaPct"),
            DailyBollingerPercentB = ReadDecimal(indicators, "BB_20_2_D", "percentB"),
            WeeklyBollingerPercentB = ReadDecimal(indicators, "BB_20_2_W", "percentB"),
            CompositeScore = score?.Score,
            MatchedSignals = matchedSignals ?? []
        };
    }

    private static bool PassesFilters(
        ScreenerRowDto row,
        ScreenerQuery query,
        List<string>? matchedSignals)
    {
        if (!string.IsNullOrWhiteSpace(query.Signal))
        {
            if (matchedSignals is null || !matchedSignals.Contains(query.Signal))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(query.BmsbStatus) &&
            !string.Equals(row.BmsbStatus, query.BmsbStatus, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!WithinRange(row.DailyRsi, query.MinDailyRsi, query.MaxDailyRsi))
        {
            return false;
        }

        if (!WithinRange(row.WeeklyRsi, query.MinWeeklyRsi, query.MaxWeeklyRsi))
        {
            return false;
        }

        if (!WithinRange(row.CompositeScore, query.MinCompositeScore, query.MaxCompositeScore))
        {
            return false;
        }

        if (!WithinRange(row.DailyBollingerPercentB, query.MinBollingerPercentB, query.MaxBollingerPercentB))
        {
            return false;
        }

        return true;
    }

    private static bool WithinRange(decimal? value, decimal? min, decimal? max)
    {
        if (value is null)
        {
            return min is null && max is null;
        }

        if (min is not null && value < min)
        {
            return false;
        }

        if (max is not null && value > max)
        {
            return false;
        }

        return true;
    }

    private static List<ScreenerRowDto> ApplySort(List<ScreenerRowDto> rows, string sort)
        => sort.ToLowerInvariant() switch
        {
            "symbolasc" => rows.OrderBy(r => r.Symbol).ToList(),
            "symboldesc" => rows.OrderByDescending(r => r.Symbol).ToList(),
            "marketcapdesc" => rows.OrderByDescending(r => r.MarketCap).ToList(),
            "marketcapasc" => rows.OrderBy(r => r.MarketCap).ToList(),
            "dailyrsiasc" => rows.OrderBy(r => r.DailyRsi).ToList(),
            "dailyrsidesc" => rows.OrderByDescending(r => r.DailyRsi).ToList(),
            "compositescoreasc" => rows.OrderBy(r => r.CompositeScore).ToList(),
            _ => rows.OrderByDescending(r => r.CompositeScore).ThenBy(r => r.Symbol).ToList()
        };

    private static string? ReadString(Dictionary<string, JsonElement> indicators, string code, string field)
    {
        if (!indicators.TryGetValue(code, out var values) || !values.TryGetProperty(field, out var element))
        {
            return null;
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
    }

    private static decimal? ReadDecimal(Dictionary<string, JsonElement> indicators, string code, string field)
    {
        if (!indicators.TryGetValue(code, out var values) || !values.TryGetProperty(field, out var element))
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetDecimal(),
            JsonValueKind.True => 1,
            JsonValueKind.False => 0,
            _ => decimal.TryParse(element.ToString(), out var parsed) ? parsed : null
        };
    }
}
