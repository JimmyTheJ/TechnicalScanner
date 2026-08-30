using System.Text.Json;
using TechnicalScanner.Application.Models.Profiles;
using TechnicalScanner.Application.Profiles;
using TechnicalScanner.Application.Signals;

namespace TechnicalScanner.Tests.Profiles;

public class BreadthAggregationTests
{
    [Fact]
    public void ProfileMetricCode_RoundTrips()
    {
        var code = BreadthAggregation.ProfileMetricCode("deep_pullback");

        Assert.Equal("PROFILE_DEEP_PULLBACK_PCT", code);
        Assert.True(BreadthAggregation.IsProfileMetric(code));
        Assert.Equal("DEEP_PULLBACK", BreadthAggregation.ProfileCodeFromMetric(code));
        Assert.False(BreadthAggregation.IsProfileMetric("BMSB_W_BELOW_PCT"));
    }

    [Fact]
    public void Tally_ComputesPercentAndStats()
    {
        var tally = BreadthAggregation.Tally(4, 1, [1, 2, 3, 4]);

        Assert.Equal(25m, tally.MatchPct);
        Assert.Equal(2.5m, tally.Median);
        Assert.Equal(2.5m, tally.Average);
        Assert.Equal(0m, BreadthAggregation.Tally(0, 0, []).MatchPct);
    }

    [Fact]
    public void UsesScoreFallback_EmptyAllRule_IsTrue()
    {
        var evaluator = new SignalRuleEvaluator();

        Assert.True(BreadthAggregation.UsesScoreFallback("""{"all":[]}""", evaluator));
        Assert.False(BreadthAggregation.UsesScoreFallback(
            """{"indicatorCode":"RSI_14_D","field":"rsi","operator":"crossed_above","value":30}""",
            evaluator));
        Assert.False(BreadthAggregation.UsesScoreFallback("""{"any":[]}""", evaluator));
    }

    [Fact]
    public void ScoreFallback_CountsPositiveWeightsOnly()
    {
        var evaluator = new SignalRuleEvaluator();
        var weights = new List<ScoreWeightSpec>
        {
            new()
            {
                Label = "oversold",
                IndicatorCode = "RSI_14_D",
                Field = "rsi",
                Operator = "<",
                Value = JsonSerializer.SerializeToElement(30),
                Weight = 1
            }
        };

        var cheap = new Dictionary<string, Dictionary<string, JsonElement>>
        {
            ["RSI_14_D"] = new() { ["rsi"] = JsonSerializer.SerializeToElement(20m) }
        };
        var expensive = new Dictionary<string, Dictionary<string, JsonElement>>
        {
            ["RSI_14_D"] = new() { ["rsi"] = JsonSerializer.SerializeToElement(70m) }
        };

        Assert.True(ProfileScoring.Score(weights, cheap, evaluator).Score > 0);
        Assert.Equal(0m, ProfileScoring.Score(weights, expensive, evaluator).Score);
    }
}
