using System.Text.Json;
using TechnicalScanner.Application.Models.Profiles;
using TechnicalScanner.Application.Profiles;
using TechnicalScanner.Application.Signals;

namespace TechnicalScanner.Tests.Profiles;

public class ProfileScoringAndReplayTests
{
    [Fact]
    public void Score_AddsMatchingWeights()
    {
        var evaluator = new SignalRuleEvaluator();
        var values = new Dictionary<string, Dictionary<string, JsonElement>>
        {
            ["SMA_200_D"] = new()
            {
                ["distanceFromSmaPct"] = JsonSerializer.SerializeToElement(-12m)
            },
            ["RSI_14_D"] = new()
            {
                ["rsi"] = JsonSerializer.SerializeToElement(25m)
            }
        };
        var weights = new List<ScoreWeightSpec>
        {
            Weight("deep", "SMA_200_D", "distanceFromSmaPct", "<", 0, 2),
            Weight("oversold", "RSI_14_D", "rsi", "<", 30, 1),
            Weight("hot", "RSI_14_D", "rsi", ">", 70, 5)
        };

        var (score, components) = ProfileScoring.Score(weights, values, evaluator);

        Assert.Equal(3m, score);
        Assert.Equal(2, components.Count);
        Assert.False(components.ContainsKey("hot"));
    }

    [Fact]
    public void Replay_CountsEntriesOnTransitionNotEveryMatchDay()
    {
        var dates = new[]
        {
            new DateOnly(2024, 1, 2),
            new DateOnly(2024, 1, 3),
            new DateOnly(2024, 1, 4),
            new DateOnly(2024, 1, 5)
        };
        var series = new Dictionary<string, IReadOnlyList<(DateOnly Date, Dictionary<string, JsonElement> Fields)>>
        {
            ["RSI_14_D"] =
            [
                (dates[0], Fields(25)),
                (dates[1], Fields(28)),
                (dates[2], Fields(55)),
                (dates[3], Fields(20))
            ]
        };
        var closes = dates.Select(d => (d, 100m)).ToList();
        var rule = """
            {"indicatorCode":"RSI_14_D","field":"rsi","operator":"<","value":30}
            """;

        var result = ProfileReplay.Replay(
            "TEST",
            "AAA",
            rule,
            [],
            dates,
            series,
            closes,
            forwardDays: 1);

        Assert.Equal(3, result.MatchDays);
        Assert.Equal(2, result.EntryCount);
        Assert.Equal(new DateOnly(2024, 1, 2), result.Entries[0].Date);
        Assert.Equal(new DateOnly(2024, 1, 5), result.Entries[1].Date);
    }

    [Fact]
    public void Stats_MedianAndMean_HandleEvenOddAndEmpty()
    {
        Assert.Null(ProfileBacktestStats.Median([]));
        Assert.Null(ProfileBacktestStats.Mean([]));
        Assert.Equal(2m, ProfileBacktestStats.Median([1, 2, 3]));
        Assert.Equal(2.5m, ProfileBacktestStats.Median([1, 2, 3, 4]));
        Assert.Equal(2m, ProfileBacktestStats.Mean([1, 2, 3]));
    }

    private static ScoreWeightSpec Weight(
        string label,
        string code,
        string field,
        string op,
        object value,
        decimal weight)
        => new()
        {
            Label = label,
            IndicatorCode = code,
            Field = field,
            Operator = op,
            Value = JsonSerializer.SerializeToElement(value),
            Weight = weight
        };

    private static Dictionary<string, JsonElement> Fields(decimal rsi)
        => new() { ["rsi"] = JsonSerializer.SerializeToElement(rsi) };
}
