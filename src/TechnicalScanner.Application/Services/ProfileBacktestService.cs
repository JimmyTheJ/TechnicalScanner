using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Models.Profiles;
using TechnicalScanner.Application.Profiles;
using TechnicalScanner.Application.Services.Queries;
using TechnicalScanner.Application.Signals;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Services;

public sealed class ProfileBacktestService(
    ITechnicalScannerDbContext dbContext,
    ScanProfileQueryService scanProfileQueryService)
{
    private const int SampleEntryLimit = 100;
    private const int HitFlushBatchSize = 200;

    public ProfileBacktestResultDto Replay(
        string profileCode,
        string symbol,
        int forwardDays,
        DateOnly? from,
        DateOnly? to)
    {
        var profile = scanProfileQueryService.Find(profileCode)
            ?? throw new InvalidOperationException($"Profile '{profileCode}' was not found.");

        var ticker = dbContext.Tickers.FirstOrDefault(t => t.Symbol == symbol && t.IsActive)
            ?? throw new InvalidOperationException($"Ticker '{symbol}' was not found.");

        var codes = ProfileIndicatorCodes(profile);
        return ReplayTicker(profile, ticker, codes, LoadDefinitions(codes), Math.Clamp(forwardDays, 1, 252), from, to);
    }

    public ProfileTickerSnapshotDto LiveSnapshot(string profileCode, string symbol)
    {
        var profile = scanProfileQueryService.Find(profileCode)
            ?? throw new InvalidOperationException($"Profile '{profileCode}' was not found.");
        var ticker = dbContext.Tickers.FirstOrDefault(t => t.Symbol == symbol && t.IsActive)
            ?? throw new InvalidOperationException($"Ticker '{symbol}' was not found.");

        var codes = ProfileIndicatorCodes(profile);
        var pair = LatestIndicatorLookup.LoadCurrentAndPrevious(dbContext, [ticker.Id], codes);
        var currentSnaps = pair.Current.GetValueOrDefault(ticker.Id) ?? [];
        var previousSnaps = pair.Previous.GetValueOrDefault(ticker.Id);
        var current = ToEvaluatorMap(currentSnaps);
        var previous = ToEvaluatorMap(previousSnaps);
        var evaluator = new SignalRuleEvaluator();
        var specs = ProfileJson.Deserialize<List<ProfileIndicatorSpec>>(profile.IndicatorsJson);
        var weights = ProfileJson.Deserialize<List<ScoreWeightSpec>>(profile.ScoreWeightsJson);
        var match = evaluator.Evaluate(profile.RuleJson, current, previous);
        var score = weights.Count == 0 ? (decimal?)null : ProfileScoring.Score(weights, current, evaluator, previous).Score;

        return new ProfileTickerSnapshotDto
        {
            ProfileCode = profile.Code,
            Symbol = ticker.Symbol,
            ProfileMatch = match,
            Score = score,
            Components = ProfileScoring.Explain(weights, current, evaluator, previous),
            Metrics = specs.Select(spec => currentSnaps.TryGetValue(spec.Code, out var snapshot)
                ? new ScreenerMetricDto
                {
                    Code = spec.Code,
                    PrimaryValue = snapshot.PrimaryValue,
                    SecondaryValue = snapshot.SecondaryValue
                }
                : new ScreenerMetricDto { Code = spec.Code }).ToList()
        };
    }

    public ProfileUniverseBacktestDto? GetLatestUniverse(string profileCode)
    {
        var run = dbContext.ProfileBacktestRuns
            .Where(r => r.ProfileCode == profileCode)
            .OrderByDescending(r => r.StartedUtc)
            .FirstOrDefault();
        return run is null ? null : ToDto(run);
    }

    public async Task<ProfileUniverseBacktestDto> RunUniverseAsync(
        string profileCode,
        int forwardDays,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        var profile = scanProfileQueryService.Find(profileCode)
            ?? throw new InvalidOperationException($"Profile '{profileCode}' was not found.");

        var universe = SafeUniverse(profile.UniverseJson);
        var tickers = ProfileUniverseQuery
            .ApplyTickerFilters(dbContext.Tickers.Where(t => t.IsActive), universe)
            .OrderBy(t => t.Symbol)
            .ToList();
        var tickerIds = ProfileUniverseQuery.ApplyMarketFilters(
            dbContext,
            tickers.Select(t => t.Id).ToHashSet(),
            universe);
        tickers = tickers.Where(t => tickerIds.Contains(t.Id)).ToList();

        var clampedForward = Math.Clamp(forwardDays, 1, 252);
        var run = new ProfileBacktestRun
        {
            ScanProfileId = profile.Id,
            ProfileCode = profile.Code,
            ForwardDays = clampedForward,
            FromDate = from,
            ToDate = to,
            StartedUtc = DateTimeOffset.UtcNow,
            Status = "Running",
            TickerCount = tickers.Count
        };
        dbContext.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var pendingHits = new List<ProfileBacktestHit>();
            var tickersWithEntries = 0;
            var entryCount = 0;
            var matchDays = 0;
            var forwardReturns = new List<decimal>();
            var codes = ProfileIndicatorCodes(profile);
            var definitions = LoadDefinitions(codes);

            foreach (var ticker in tickers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = ReplayTicker(profile, ticker, codes, definitions, clampedForward, from, to);
                matchDays += result.MatchDays;
                if (result.EntryCount == 0)
                {
                    continue;
                }

                tickersWithEntries++;
                entryCount += result.EntryCount;
                foreach (var entry in result.Entries)
                {
                    if (entry.ForwardReturnPct is { } fwd)
                    {
                        forwardReturns.Add(fwd);
                    }

                    pendingHits.Add(new ProfileBacktestHit
                    {
                        ProfileBacktestRunId = run.Id,
                        TickerId = ticker.Id,
                        Symbol = ticker.Symbol,
                        EntryDate = entry.Date,
                        Close = entry.Close,
                        ForwardReturnPct = entry.ForwardReturnPct,
                        Score = entry.Score
                    });
                }

                if (pendingHits.Count >= HitFlushBatchSize)
                {
                    await FlushHitsAsync(pendingHits, cancellationToken);
                }
            }

            await FlushHitsAsync(pendingHits, cancellationToken);

            run.Status = "Completed";
            run.CompletedUtc = DateTimeOffset.UtcNow;
            run.TickersWithEntries = tickersWithEntries;
            run.EntryCount = entryCount;
            run.MatchDays = matchDays;
            run.MedianForwardReturnPct = ProfileBacktestStats.Median(forwardReturns);
            run.MeanForwardReturnPct = ProfileBacktestStats.Mean(forwardReturns);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ToDto(run);
        }
        catch (Exception ex)
        {
            run.Status = "Failed";
            run.CompletedUtc = DateTimeOffset.UtcNow;
            run.ErrorMessage = ex.Message;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private ProfileBacktestResultDto ReplayTicker(
        ScanProfile profile,
        Ticker ticker,
        IReadOnlyList<string> codes,
        IReadOnlyDictionary<long, string> definitions,
        int forwardDays,
        DateOnly? from,
        DateOnly? to)
    {
        var series = new Dictionary<string, IReadOnlyList<(DateOnly Date, Dictionary<string, System.Text.Json.JsonElement> Fields)>>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var code in codes)
        {
            series[code] = [];
        }

        var definitionIds = definitions.Keys.ToList();
        var values = definitionIds.Count == 0
            ? []
            : dbContext.IndicatorValues
                .Where(v => v.TickerId == ticker.Id && definitionIds.Contains(v.IndicatorDefinitionId))
                .OrderBy(v => v.CandleDate)
                .ToList();

        foreach (var group in values.GroupBy(v => definitions[v.IndicatorDefinitionId]))
        {
            series[group.Key] = group
                .Select(v => (v.CandleDate, IndicatorValueMaps.ParseValuesJson(v.ValuesJson)))
                .ToList();
        }

        var daily = dbContext.PriceCandles
            .Where(p => p.TickerId == ticker.Id && p.Timeframe == "D")
            .OrderBy(p => p.CandleDate)
            .ToList();

        if (from is not null)
        {
            daily = daily.Where(p => p.CandleDate >= from.Value).ToList();
        }

        if (to is not null)
        {
            daily = daily.Where(p => p.CandleDate <= to.Value).ToList();
        }

        var weights = ProfileJson.Deserialize<List<ScoreWeightSpec>>(profile.ScoreWeightsJson);
        var dates = daily.Select(p => p.CandleDate).ToList();
        var closes = daily.Select(p => (p.CandleDate, p.AdjustedClose ?? p.Close)).ToList();

        return ProfileReplay.Replay(
            profile.Code,
            ticker.Symbol,
            profile.RuleJson,
            weights,
            dates,
            series,
            closes,
            forwardDays);
    }

    private static IReadOnlyList<string> ProfileIndicatorCodes(ScanProfile profile)
    {
        var specs = ProfileJson.Deserialize<List<ProfileIndicatorSpec>>(profile.IndicatorsJson);
        var weights = ProfileJson.Deserialize<List<ScoreWeightSpec>>(profile.ScoreWeightsJson);
        return specs.Select(s => s.Code)
            .Concat(weights.Select(w => w.IndicatorCode))
            .Concat(SignalRuleEvaluator.CollectIndicatorCodes(profile.RuleJson))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private Dictionary<long, string> LoadDefinitions(IReadOnlyList<string> codes)
    {
        if (codes.Count == 0)
        {
            return [];
        }

        var codeList = codes.ToList();
        return dbContext.IndicatorDefinitions
            .Where(d => codeList.Contains(d.Code))
            .ToList()
            .ToDictionary(d => d.Id, d => d.Code);
    }

    private async Task FlushHitsAsync(List<ProfileBacktestHit> hits, CancellationToken cancellationToken)
    {
        if (hits.Count == 0)
        {
            return;
        }

        foreach (var hit in hits)
        {
            dbContext.Add(hit);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        hits.Clear();
    }

    private ProfileUniverseBacktestDto ToDto(ProfileBacktestRun run)
    {
        var sample = dbContext.ProfileBacktestHits
            .Where(h => h.ProfileBacktestRunId == run.Id)
            .OrderByDescending(h => h.Score)
            .ThenByDescending(h => h.EntryDate)
            .Take(SampleEntryLimit)
            .Select(h => new ProfileUniverseBacktestHitDto
            {
                Symbol = h.Symbol,
                EntryDate = h.EntryDate,
                Close = h.Close,
                ForwardReturnPct = h.ForwardReturnPct,
                Score = h.Score
            })
            .ToList();

        return new ProfileUniverseBacktestDto
        {
            RunId = run.Id,
            ProfileCode = run.ProfileCode,
            Status = run.Status,
            StartedUtc = run.StartedUtc,
            CompletedUtc = run.CompletedUtc,
            ForwardDays = run.ForwardDays,
            FromDate = run.FromDate,
            ToDate = run.ToDate,
            TickerCount = run.TickerCount,
            TickersWithEntries = run.TickersWithEntries,
            EntryCount = run.EntryCount,
            MatchDays = run.MatchDays,
            MedianForwardReturnPct = run.MedianForwardReturnPct,
            MeanForwardReturnPct = run.MeanForwardReturnPct,
            ErrorMessage = run.ErrorMessage,
            SampleEntries = sample
        };
    }

    private static ProfileUniverse SafeUniverse(string json)
    {
        try
        {
            return ProfileJson.Deserialize<ProfileUniverse>(json);
        }
        catch (System.Text.Json.JsonException)
        {
            return new ProfileUniverse();
        }
    }

    private static Dictionary<string, Dictionary<string, System.Text.Json.JsonElement>> ToEvaluatorMap(
        Dictionary<string, LatestIndicatorSnapshot>? snapshots)
    {
        var result = new Dictionary<string, Dictionary<string, System.Text.Json.JsonElement>>(
            StringComparer.OrdinalIgnoreCase);
        if (snapshots is null)
        {
            return result;
        }

        foreach (var (code, snapshot) in snapshots)
        {
            result[code] = IndicatorValueMaps.ToFields(snapshot.Values);
        }

        return result;
    }
}
