using System.Text.Json;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Models.Profiles;
using TechnicalScanner.Application.Models.Queries;
using TechnicalScanner.Application.Profiles;
using TechnicalScanner.Application.Signals;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Services.Queries;

public sealed class ScreenerQueryService(
    ITechnicalScannerDbContext dbContext,
    SignalRuleEvaluator ruleEvaluator)
{
    public PagedResult<ScreenerRowDto> GetScreener(ScreenerQuery query, CancellationToken cancellationToken)
    {
        var (page, pageSize, skip) = QueryPaging.Normalize(query.Page, query.PageSize);
        var latestScanDate = dbContext.SignalResults
            .OrderByDescending(r => r.ScanDateUtc)
            .Select(r => (DateTimeOffset?)r.ScanDateUtc)
            .FirstOrDefault();

        var profile = LoadProfile(query.Profile);
        var universe = profile is null ? new ProfileUniverse() : SafeUniverse(profile.UniverseJson);
        var profileIndicators = profile is null ? [] : SafeIndicators(profile.IndicatorsJson);
        var weights = profile is null ? [] : SafeWeights(profile.ScoreWeightsJson);

        var tickers = ApplyTickerFilters(dbContext.Tickers.Where(t => t.IsActive), query, universe).ToList();
        if (tickers.Count == 0)
        {
            return new PagedResult<ScreenerRowDto>([], page, pageSize, 0);
        }

        var tickerIds = tickers.Select(t => t.Id).ToHashSet();
        if (query.ParsedSymbols().Count == 0)
        {
            tickerIds = ProfileUniverseQuery.ApplyMarketFilters(dbContext, tickerIds, universe);
        }
        tickerIds = ApplyLatestPrimaryFilters(tickerIds, query);
        tickers = tickers.Where(t => tickerIds.Contains(t.Id)).ToList();
        if (tickers.Count == 0)
        {
            return new PagedResult<ScreenerRowDto>([], page, pageSize, 0);
        }

        var neededCodes = RequiredScreenerCodes(profile, profileIndicators, weights);
        var needsPrevious = profile is not null && (
            SignalRuleEvaluator.RequiresPrevious(profile.RuleJson) ||
            weights.Any(weight => SignalRuleEvaluator.IsTemporalOperator(weight.Operator)));
        var pair = needsPrevious
            ? LatestIndicatorLookup.LoadCurrentAndPrevious(dbContext, tickerIds, neededCodes)
            : new LatestPair(LatestIndicatorLookup.Load(dbContext, tickerIds, neededCodes), []);
        var indicatorLookup = pair.Current;
        var previousLookup = pair.Previous;
        var matchedSignals = BuildMatchedSignals(latestScanDate, tickerIds);
        var scoreCode = profile?.Code ?? "DEEP_PULLBACK";
        var scores = BuildLatestScores(tickerIds, scoreCode);

        var rows = new List<ScreenerRowDto>();
        foreach (var ticker in tickers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshots = indicatorLookup.GetValueOrDefault(ticker.Id) ?? [];
            var valuesByCode = snapshots.ToDictionary(
                x => x.Key,
                x => x.Value.Values,
                StringComparer.OrdinalIgnoreCase);
            var evaluatorMap = IndicatorValueMaps.ToEvaluatorMap(valuesByCode);
            var previousMap = LatestIndicatorLookup.ToEvaluatorMap(previousLookup.GetValueOrDefault(ticker.Id));

            bool? profileMatch = null;
            if (profile is not null)
            {
                profileMatch = ruleEvaluator.Evaluate(profile.RuleJson, evaluatorMap, previousMap);
                if (query.ProfileMatchOnly && profileMatch == false)
                {
                    continue;
                }
            }

            decimal? liveScore = null;
            if (weights.Count > 0)
            {
                liveScore = ProfileScoring.Score(weights, evaluatorMap, ruleEvaluator, previousMap).Score;
            }

            decimal? persistedScore = scores.TryGetValue(ticker.Id, out var stored) ? stored.Score : null;

            var row = BuildRow(
                ticker,
                valuesByCode,
                snapshots,
                profileIndicators,
                matchedSignals.GetValueOrDefault(ticker.Id),
                liveScore ?? persistedScore,
                profileMatch,
                profile?.Code);
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

    private ScanProfile? LoadProfile(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return dbContext.ScanProfiles.FirstOrDefault(p => p.IsActive && p.Code == code);
    }

    private static IQueryable<Ticker> ApplyTickerFilters(
        IQueryable<Ticker> query,
        ScreenerQuery filters,
        ProfileUniverse universe)
    {
        var symbols = filters.ParsedSymbols().ToList();
        if (symbols.Count > 0)
        {
            query = query.Where(t => symbols.Contains(t.Symbol));
        }

        var country = symbols.Count > 0 ? filters.Country : (filters.Country ?? universe.Country);
        if (!string.IsNullOrWhiteSpace(country))
        {
            query = query.Where(t => t.Country == country);
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

        if (symbols.Count == 0 && !universe.IncludeEtfs)
        {
            query = query.Where(t => !t.IsEtf);
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

    private HashSet<long> ApplyLatestPrimaryFilters(HashSet<long> tickerIds, ScreenerQuery query)
    {
        tickerIds = FilterByLatestPrimary(tickerIds, "RSI_14_D", query.MinDailyRsi, query.MaxDailyRsi);
        tickerIds = FilterByLatestPrimary(tickerIds, "RSI_14_W", query.MinWeeklyRsi, query.MaxWeeklyRsi);
        tickerIds = FilterByLatestPrimary(tickerIds, "BB_20_2_D", query.MinBollingerPercentB, query.MaxBollingerPercentB);
        return tickerIds;
    }

    private HashSet<long> FilterByLatestPrimary(
        HashSet<long> tickerIds,
        string indicatorCode,
        decimal? min,
        decimal? max)
    {
        if (tickerIds.Count == 0 || (min is null && max is null))
        {
            return tickerIds;
        }

        var definitionId = dbContext.IndicatorDefinitions
            .Where(d => d.Code == indicatorCode)
            .Select(d => (long?)d.Id)
            .FirstOrDefault();
        if (definitionId is null)
        {
            return tickerIds;
        }

        var tickerIdList = tickerIds.ToList();
        var latestDates = dbContext.IndicatorValues
            .Where(v => v.IndicatorDefinitionId == definitionId && tickerIdList.Contains(v.TickerId))
            .GroupBy(v => v.TickerId)
            .Select(g => new { TickerId = g.Key, Date = g.Max(v => v.CandleDate) });

        var values = from value in dbContext.IndicatorValues
                     join latest in latestDates on new { value.TickerId, value.CandleDate }
                         equals new { latest.TickerId, CandleDate = latest.Date }
                     where value.IndicatorDefinitionId == definitionId
                     select value;

        if (min is not null)
        {
            values = values.Where(v => v.PrimaryValue >= min);
        }

        if (max is not null)
        {
            values = values.Where(v => v.PrimaryValue <= max);
        }

        return values.Select(v => v.TickerId).ToHashSet();
    }

    private static IReadOnlyList<string> RequiredScreenerCodes(
        ScanProfile? profile,
        IReadOnlyList<ProfileIndicatorSpec> profileIndicators,
        IReadOnlyList<ScoreWeightSpec> weights)
    {
        var codes = new HashSet<string>(DisplayIndicatorCodes, StringComparer.OrdinalIgnoreCase);
        foreach (var spec in profileIndicators)
        {
            codes.Add(spec.Code);
        }

        foreach (var weight in weights)
        {
            codes.Add(weight.IndicatorCode);
        }

        if (profile is not null)
        {
            foreach (var code in SignalRuleEvaluator.CollectIndicatorCodes(profile.RuleJson))
            {
                codes.Add(code);
            }
        }

        return codes.ToList();
    }

    private static readonly string[] DisplayIndicatorCodes =
    [
        "BMSB_W",
        "RSI_14_D",
        "RSI_14_W",
        "RSI_14_M",
        "SMA_200_D",
        "SMA_50_W",
        "SMA_100_W",
        "SMA_200_W",
        "BB_20_2_D",
        "BB_20_2_W"
    ];

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

    private Dictionary<long, (decimal Score, string ComponentsJson)> BuildLatestScores(HashSet<long> tickerIds, string scoreCode)
    {
        return dbContext.ScanScores
            .Where(s => tickerIds.Contains(s.TickerId) && s.ScoreCode == scoreCode)
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
        Dictionary<string, LatestIndicatorSnapshot> snapshots,
        IReadOnlyList<ProfileIndicatorSpec> profileIndicators,
        List<string>? matchedSignals,
        decimal? score,
        bool? profileMatch,
        string? profileCode)
    {
        var metrics = profileIndicators
            .Select(spec => snapshots.TryGetValue(spec.Code, out var snapshot)
                ? new ScreenerMetricDto
                {
                    Code = spec.Code,
                    PrimaryValue = snapshot.PrimaryValue,
                    SecondaryValue = snapshot.SecondaryValue
                }
                : new ScreenerMetricDto { Code = spec.Code })
            .ToList();

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
            CompositeScore = score,
            ProfileMatch = profileMatch,
            ProfileCode = profileCode,
            Metrics = metrics,
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
        if (!indicators.TryGetValue(code, out var values) ||
            values.ValueKind != JsonValueKind.Object ||
            !values.TryGetProperty(field, out var element))
        {
            return null;
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
    }

    private static decimal? ReadDecimal(Dictionary<string, JsonElement> indicators, string code, string field)
    {
        if (!indicators.TryGetValue(code, out var values) ||
            values.ValueKind != JsonValueKind.Object ||
            !values.TryGetProperty(field, out var element))
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

    private static ProfileUniverse SafeUniverse(string json)
    {
        try
        {
            return ProfileJson.Deserialize<ProfileUniverse>(json);
        }
        catch (JsonException)
        {
            return new ProfileUniverse();
        }
    }

    private static List<ProfileIndicatorSpec> SafeIndicators(string json)
    {
        try
        {
            return ProfileJson.Deserialize<List<ProfileIndicatorSpec>>(json);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<ScoreWeightSpec> SafeWeights(string json)
    {
        try
        {
            return ProfileJson.Deserialize<List<ScoreWeightSpec>>(json);
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
