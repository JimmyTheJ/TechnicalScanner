using Microsoft.Extensions.Logging;
using System.Text.Json;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Models.Profiles;
using TechnicalScanner.Application.Profiles;
using TechnicalScanner.Application.Services.Queries;
using TechnicalScanner.Application.Signals;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Services;

public sealed class CompositeScoringService(
    ITechnicalScannerDbContext dbContext,
    SignalRuleEvaluator ruleEvaluator,
    ILogger<CompositeScoringService> logger)
{
    public async Task<int> CalculateAsync(long? scanRunId = null, CancellationToken cancellationToken = default)
    {
        var scanDate = DateTimeOffset.UtcNow;
        var tickers = dbContext.Tickers.Where(t => t.IsActive).ToList();
        var profiles = dbContext.ScanProfiles.Where(p => p.IsActive).ToList();
        var recipes = profiles
            .Select(p => (
                Code: p.Code,
                Weights: SafeWeights(p.ScoreWeightsJson)))
            .Where(p => p.Weights.Count > 0)
            .ToList();

        if (recipes.Count == 0)
        {
            recipes.Add(("DEEP_PULLBACK", LegacyDeepPullbackWeights()));
        }

        if (tickers.Count == 0)
        {
            return 0;
        }

        var codes = recipes
            .SelectMany(recipe => recipe.Weights.Select(weight => weight.IndicatorCode))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var needsPrevious = recipes.Any(recipe =>
            recipe.Weights.Any(weight => SignalRuleEvaluator.IsTemporalOperator(weight.Operator)));
        var tickerIds = tickers.Select(t => t.Id).ToList();
        var pair = needsPrevious
            ? LatestIndicatorLookup.LoadCurrentAndPrevious(dbContext, tickerIds, codes)
            : new LatestPair(LatestIndicatorLookup.Load(dbContext, tickerIds, codes), []);

        var count = 0;
        foreach (var ticker in tickers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = LatestIndicatorLookup.ToEvaluatorMap(pair.Current.GetValueOrDefault(ticker.Id));
            var previous = LatestIndicatorLookup.ToEvaluatorMap(pair.Previous.GetValueOrDefault(ticker.Id));

            foreach (var (code, weights) in recipes)
            {
                var (score, components) = ProfileScoring.Score(weights, current, ruleEvaluator, previous);
                dbContext.Add(new ScanScore
                {
                    ScanRunId = scanRunId,
                    ScanDateUtc = scanDate,
                    TickerId = ticker.Id,
                    ScoreCode = code,
                    Score = score,
                    ComponentsJson = JsonSerializer.Serialize(components)
                });
                count++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Calculated {Count} composite scores.", count);
        return count;
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

    private static List<ScoreWeightSpec> LegacyDeepPullbackWeights() =>
    [
        Weight("belowBmsb", "BMSB_W", "status", "=", "BelowBand", 2),
        Weight("dailyRsiOversold", "RSI_14_D", "rsi", "<", 30, 1),
        Weight("weeklyRsiWeak", "RSI_14_W", "rsi", "<", 40, 1),
        Weight("below200dSma", "SMA_200_D", "distanceFromSmaPct", "<", 0, 1),
        Weight("below50wSma", "SMA_50_W", "distanceFromSmaPct", "<", 0, 1),
        Weight("belowDailyBbLower", "BB_20_2_D", "status", "=", "CloseBelowLowerBand", 1)
    ];

    private static ScoreWeightSpec Weight(
        string label,
        string indicatorCode,
        string field,
        string op,
        object value,
        decimal weight)
        => new()
        {
            Label = label,
            IndicatorCode = indicatorCode,
            Field = field,
            Operator = op,
            Value = JsonSerializer.SerializeToElement(value),
            Weight = weight
        };
}
