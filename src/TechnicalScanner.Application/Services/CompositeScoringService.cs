using Microsoft.Extensions.Logging;
using System.Text.Json;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Models.Profiles;
using TechnicalScanner.Application.Profiles;
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

        var count = 0;
        foreach (var ticker in tickers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = GetLatestIndicatorValues(ticker.Id);

            foreach (var (code, weights) in recipes)
            {
                var (score, components) = ProfileScoring.Score(weights, values, ruleEvaluator);
                UpsertScore(scanRunId, scanDate, ticker.Id, code, score, components);
                count++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Calculated {Count} composite scores.", count);
        return count;
    }

    private void UpsertScore(
        long? scanRunId,
        DateTimeOffset scanDate,
        long tickerId,
        string scoreCode,
        decimal score,
        Dictionary<string, decimal> components)
    {
        var existing = dbContext.ScanScores.FirstOrDefault(s =>
            s.ScanDateUtc == scanDate && s.TickerId == tickerId && s.ScoreCode == scoreCode);

        var componentsJson = JsonSerializer.Serialize(components);
        if (existing is null)
        {
            dbContext.Add(new ScanScore
            {
                ScanRunId = scanRunId,
                ScanDateUtc = scanDate,
                TickerId = tickerId,
                ScoreCode = scoreCode,
                Score = score,
                ComponentsJson = componentsJson
            });
        }
        else
        {
            existing.ScanRunId = scanRunId;
            existing.Score = score;
            existing.ComponentsJson = componentsJson;
        }
    }

    private Dictionary<string, Dictionary<string, JsonElement>> GetLatestIndicatorValues(long tickerId)
    {
        return dbContext.IndicatorValues
            .Where(v => v.TickerId == tickerId)
            .Join(
                dbContext.IndicatorDefinitions,
                value => value.IndicatorDefinitionId,
                definition => definition.Id,
                (value, definition) => new { definition.Code, value })
            .GroupBy(x => x.Code)
            .Select(g => g.OrderByDescending(x => x.value.CandleDate).First())
            .ToDictionary(
                x => x.Code,
                x => IndicatorValueMaps.ParseValuesJson(x.value.ValuesJson),
                StringComparer.OrdinalIgnoreCase);
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
