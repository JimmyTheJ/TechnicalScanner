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
        var recipes = dbContext.ScanProfiles
            .Where(p => p.IsActive)
            .AsEnumerable()
            .Select(p => new ProfileScoreRecipe(p.Code, p.RuleJson, SafeWeights(p.ScoreWeightsJson)))
            .ToList();

        if (recipes.Count == 0)
        {
            recipes.Add(new ProfileScoreRecipe("DEEP_PULLBACK", """{"all":[]}""", LegacyDeepPullbackWeights()));
        }

        if (tickers.Count == 0)
        {
            return 0;
        }

        var codes = recipes
            .SelectMany(recipe => recipe.Weights.Select(weight => weight.IndicatorCode)
                .Concat(SignalRuleEvaluator.CollectIndicatorCodes(recipe.RuleJson)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var needsPrevious = recipes.Any(recipe =>
            SignalRuleEvaluator.RequiresPrevious(recipe.RuleJson) ||
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

            foreach (var recipe in recipes)
            {
                var (score, components) = recipe.Weights.Count > 0
                    ? ProfileScoring.Score(recipe.Weights, current, ruleEvaluator, previous)
                    : (0m, new Dictionary<string, decimal>());
                if (recipe.Weights.Count == 0)
                {
                    var match = EvaluateRule(recipe.RuleJson, current, previous);
                    score = match ? 1m : 0m;
                    components["ruleMatch"] = score;
                }

                dbContext.Add(new ScanScore
                {
                    ScanRunId = scanRunId,
                    ScanDateUtc = scanDate,
                    TickerId = ticker.Id,
                    ScoreCode = recipe.Code,
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

    private bool EvaluateRule(
        string ruleJson,
        Dictionary<string, Dictionary<string, JsonElement>> current,
        Dictionary<string, Dictionary<string, JsonElement>> previous)
    {
        try
        {
            return ruleEvaluator.Evaluate(ruleJson, current, previous);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or JsonException)
        {
            return false;
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

    private sealed record ProfileScoreRecipe(
        string Code,
        string RuleJson,
        List<ScoreWeightSpec> Weights);
}
