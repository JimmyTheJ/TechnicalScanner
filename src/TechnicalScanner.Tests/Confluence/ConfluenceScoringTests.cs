using System.Text.Json;
using TechnicalScanner.Application.Confluence;
using TechnicalScanner.Application.Signals;

namespace TechnicalScanner.Tests.Confluence;

public class ConfluenceScoringTests
{
    [Fact]
    public void Evaluate_SkipsMissingIndicatorsFromValidCount()
    {
        var values = Map(("RSI_14_D", "rsi", 25m));
        var tally = ConfluenceScoring.Evaluate(values, new SignalRuleEvaluator());

        Assert.Equal(1, tally.CheapValid);
        Assert.Equal(1, tally.CheapHits);
        Assert.Equal(1, tally.ExtendedValid);
        Assert.Equal(0, tally.ExtendedHits);
        Assert.Equal(ConfluenceCatalog.Cheap, tally.Bias);
        Assert.Contains(tally.Components, c => c.Label == "Daily RSI oversold" && c.Available && c.Matched);
        Assert.Contains(tally.Components, c => c.Label == "Below 200D SMA" && !c.Available && !c.Matched);
    }

    [Fact]
    public void Evaluate_CountsDailyWeeklyMonthlyCheapAgreement()
    {
        var values = Map(
            ("RSI_14_D", "rsi", 25m),
            ("RSI_14_W", "rsi", 35m),
            ("RSI_14_M", "rsi", 45m),
            ("BMSB_W", "status", "BelowBand"),
            ("SMA_200_D", "distanceFromSmaPct", -8m),
            ("SMA_50_W", "distanceFromSmaPct", -4m),
            ("BB_20_2_D", "percentB", -0.1m),
            ("ROC_21_D", "rocPct", -3m),
            ("DRAWDOWN_ATH_D", "distanceFromAthPct", -25m));

        var tally = ConfluenceScoring.Evaluate(values, new SignalRuleEvaluator());

        Assert.Equal(9, tally.CheapValid);
        Assert.Equal(9, tally.CheapHits);
        Assert.Equal(100m, tally.CheapPct);
        Assert.Equal(7, tally.ExtendedValid);
        Assert.Equal(0, tally.ExtendedHits);
        Assert.Equal(ConfluenceCatalog.Cheap, tally.Bias);
    }

    [Fact]
    public void Evaluate_ExtendedBiasWhenAboveMovingAverage()
    {
        var values = Map(
            ("RSI_14_D", "rsi", 50m),
            ("SMA_200_D", "distanceFromSmaPct", 1m));

        var tally = ConfluenceScoring.Evaluate(values, new SignalRuleEvaluator());

        Assert.Equal(0, tally.CheapHits);
        Assert.Equal(1, tally.ExtendedHits);
        Assert.Equal(ConfluenceCatalog.Extended, tally.Bias);
    }

    private static Dictionary<string, Dictionary<string, JsonElement>> Map(
        params (string Code, string Field, object Value)[] fields)
    {
        var result = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (code, field, value) in fields)
        {
            if (!result.TryGetValue(code, out var map))
            {
                map = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                result[code] = map;
            }

            map[field] = JsonSerializer.SerializeToElement(value);
        }

        return result;
    }
}
