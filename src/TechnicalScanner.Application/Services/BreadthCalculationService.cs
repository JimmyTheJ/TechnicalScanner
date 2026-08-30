using Microsoft.Extensions.Logging;
using System.Text.Json;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Models.Profiles;
using TechnicalScanner.Application.Profiles;
using TechnicalScanner.Application.Services.Queries;
using TechnicalScanner.Application.Signals;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Services;

public sealed class BreadthCalculationService(
    ITechnicalScannerDbContext dbContext,
    SignalRuleEvaluator ruleEvaluator,
    ILogger<BreadthCalculationService> logger)
{
    private static readonly ClassicBreadthMetric[] ClassicMetrics =
    [
        new("BMSB_W_ABOVE_PCT", "BMSB_W", "status", "=", "AboveBand"),
        new("BMSB_W_BELOW_PCT", "BMSB_W", "status", "=", "BelowBand"),
        new("RSI_14_D_OVERSOLD_PCT", "RSI_14_D", "category", "=", "Oversold"),
        new("RSI_14_W_ABOVE_50_PCT", "RSI_14_W", "rsi", ">=", 50m),
        new("SMA_200_D_ABOVE_PCT", "SMA_200_D", "closeAboveSma", "=", true),
        new("SMA_50_W_ABOVE_PCT", "SMA_50_W", "closeAboveSma", "=", true),
        new("BB_20_2_D_BELOW_LOWER_PCT", "BB_20_2_D", "status", "=", "CloseBelowLowerBand")
    ];

    public async Task<int> CalculateAsync(long? scanRunId = null, CancellationToken cancellationToken = default)
    {
        var scanDate = DateTimeOffset.UtcNow;
        var tickers = dbContext.Tickers.Where(t => t.IsActive).ToList();
        var allIds = tickers.Select(t => t.Id).ToHashSet();
        var slices = BuildSlices(tickers, allIds);
        var profiles = dbContext.ScanProfiles.Where(p => p.IsActive).ToList()
            .Select(LoadProfile)
            .Where(spec => spec is not null)
            .Select(spec => spec!)
            .ToList();

        var codes = ClassicMetrics.Select(metric => metric.IndicatorCode)
            .Concat(profiles.SelectMany(profile => profile.Codes))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var needsPrevious = profiles.Any(profile => profile.NeedsPrevious)
            || ClassicMetrics.Any(metric => SignalRuleEvaluator.IsTemporalOperator(metric.Operator));
        var pair = allIds.Count == 0 || codes.Count == 0
            ? new LatestPair([], [])
            : needsPrevious
                ? LatestIndicatorLookup.LoadCurrentAndPrevious(dbContext, allIds, codes)
                : new LatestPair(LatestIndicatorLookup.Load(dbContext, allIds, codes), []);

        var count = 0;
        foreach (var slice in slices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var metric in ClassicMetrics)
            {
                AddSnapshot(scanRunId, scanDate, slice, metric.MetricCode, TallyClassic(metric, slice.Ids, pair));
                count++;
            }

            foreach (var profile in profiles)
            {
                var ids = new HashSet<long>(slice.Ids);
                ids.IntersectWith(profile.UniverseIds);
                AddSnapshot(
                    scanRunId,
                    scanDate,
                    slice,
                    BreadthAggregation.ProfileMetricCode(profile.Code),
                    TallyProfile(profile, ids, pair));
                count++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Calculated {Count} breadth metrics.", count);
        return count;
    }

    private ProfileBreadthSpec? LoadProfile(ScanProfile profile)
    {
        var universe = SafeUniverse(profile.UniverseJson);
        var ids = ProfileUniverseQuery.ApplyTickerFilters(dbContext.Tickers.Where(t => t.IsActive), universe)
            .Select(t => t.Id)
            .ToHashSet();
        ids = ProfileUniverseQuery.ApplyMarketFilters(dbContext, ids, universe);
        var weights = SafeWeights(profile.ScoreWeightsJson);
        var codes = SignalRuleEvaluator.CollectIndicatorCodes(profile.RuleJson)
            .Concat(weights.Select(weight => weight.IndicatorCode))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var scoreFallback = BreadthAggregation.UsesScoreFallback(profile.RuleJson, ruleEvaluator);
        if (scoreFallback && weights.Count == 0)
        {
            return null;
        }

        return new ProfileBreadthSpec(
            profile.Code,
            ids,
            codes,
            weights,
            profile.RuleJson,
            SignalRuleEvaluator.RequiresPrevious(profile.RuleJson)
                || weights.Any(weight => SignalRuleEvaluator.IsTemporalOperator(weight.Operator)),
            scoreFallback);
    }

    private BreadthTally TallyClassic(
        ClassicBreadthMetric metric,
        HashSet<long> tickerIds,
        LatestPair pair)
    {
        var valid = 0;
        var matches = 0;
        var values = new List<decimal>();
        var rule = ClassicRule(metric);

        foreach (var tickerId in tickerIds)
        {
            var snapshots = pair.Current.GetValueOrDefault(tickerId);
            if (snapshots is null || !snapshots.ContainsKey(metric.IndicatorCode))
            {
                continue;
            }

            valid++;
            if (snapshots[metric.IndicatorCode].PrimaryValue is { } primary)
            {
                values.Add(primary);
            }

            var current = LatestIndicatorLookup.ToEvaluatorMap(snapshots);
            var previous = LatestIndicatorLookup.ToEvaluatorMap(pair.Previous.GetValueOrDefault(tickerId));
            if (ruleEvaluator.Evaluate(rule, current, previous))
            {
                matches++;
            }
        }

        return BreadthAggregation.Tally(valid, matches, values);
    }

    private BreadthTally TallyProfile(
        ProfileBreadthSpec profile,
        HashSet<long> tickerIds,
        LatestPair pair)
    {
        var matches = 0;
        var values = new List<decimal>();
        foreach (var tickerId in tickerIds)
        {
            var current = LatestIndicatorLookup.ToEvaluatorMap(pair.Current.GetValueOrDefault(tickerId));
            var previous = LatestIndicatorLookup.ToEvaluatorMap(pair.Previous.GetValueOrDefault(tickerId));
            if (profile.ScoreFallback)
            {
                var score = ProfileScoring.Score(profile.Weights, current, ruleEvaluator, previous).Score;
                values.Add(score);
                if (score > 0)
                {
                    matches++;
                }
            }
            else if (ruleEvaluator.Evaluate(profile.RuleJson, current, previous))
            {
                matches++;
            }
        }

        return BreadthAggregation.Tally(tickerIds.Count, matches, values);
    }

    private void AddSnapshot(
        long? scanRunId,
        DateTimeOffset scanDate,
        UniverseSlice slice,
        string metricCode,
        BreadthTally tally)
    {
        dbContext.Add(new BreadthSnapshot
        {
            ScanRunId = scanRunId,
            ScanDateUtc = scanDate,
            UniverseName = slice.UniverseName,
            Country = slice.Country,
            MetricCode = metricCode,
            TotalValidStocks = tally.Valid,
            MatchCount = tally.Matches,
            MatchPct = tally.MatchPct,
            MedianValue = tally.Median,
            AverageValue = tally.Average
        });
    }

    private static List<UniverseSlice> BuildSlices(IReadOnlyList<Ticker> tickers, HashSet<long> allIds)
    {
        var slices = new List<UniverseSlice>
        {
            new(BreadthAggregation.AllUniverse, null, allIds)
        };

        foreach (var group in tickers.GroupBy(t => t.Country, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key))
        {
            if (string.IsNullOrWhiteSpace(group.Key))
            {
                continue;
            }

            slices.Add(new UniverseSlice(group.Key, group.Key, group.Select(t => t.Id).ToHashSet()));
        }

        return slices;
    }

    private static string ClassicRule(ClassicBreadthMetric metric)
        => ProfileJson.Serialize(new ComparisonCondition
        {
            IndicatorCode = metric.IndicatorCode,
            Field = metric.Field,
            Operator = metric.Operator,
            Value = JsonSerializer.SerializeToElement(metric.Threshold)
        });

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

    private sealed record ClassicBreadthMetric(
        string MetricCode,
        string IndicatorCode,
        string Field,
        string Operator,
        object Threshold);

    private sealed record ProfileBreadthSpec(
        string Code,
        HashSet<long> UniverseIds,
        IReadOnlyList<string> Codes,
        IReadOnlyList<ScoreWeightSpec> Weights,
        string RuleJson,
        bool NeedsPrevious,
        bool ScoreFallback);

    private sealed record UniverseSlice(string UniverseName, string? Country, HashSet<long> Ids);
}
