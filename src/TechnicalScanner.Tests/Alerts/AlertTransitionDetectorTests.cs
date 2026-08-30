using TechnicalScanner.Application.Alerts;
using TechnicalScanner.Application.Models.Queries;
using TechnicalScanner.Application.Options;

namespace TechnicalScanner.Tests.Alerts;

public sealed class AlertTransitionDetectorTests
{
    private static readonly AlertsOptions DefaultOptions = new()
    {
        NotifyOnSignalFired = true,
        NotifyOnSignalCleared = true,
        NotifyOnProfileEntered = true,
        NotifyOnProfileExited = true,
        CompositeScoreThreshold = 4,
        CompositeScoreCode = "DEEP_PULLBACK",
        BreadthRules =
        [
            new BreadthAlertRuleOptions
            {
                MetricCode = "BMSB_W_ABOVE_PCT",
                Threshold = 20,
                Direction = "CrossBelow"
            },
            new BreadthAlertRuleOptions
            {
                MetricCode = "BMSB_W_ABOVE_PCT",
                Threshold = 50,
                Direction = "CrossAbove"
            }
        ]
    };

    [Fact]
    public void DetectSignalTransitions_FiresWhenSignalBecomesMatch()
    {
        var previous = new[] { new SignalSnapshot("AAPL", "BMSB_ABOVE", "Above BMSB", false) };
        var current = new[] { new SignalSnapshot("AAPL", "BMSB_ABOVE", "Above BMSB", true) };

        var transitions = AlertTransitionDetector.DetectSignalTransitions(current, previous, DefaultOptions);

        Assert.Single(transitions);
        Assert.Equal("SignalFired", transitions[0].EventType);
        Assert.Equal("AAPL", transitions[0].SubjectKey);
    }

    [Fact]
    public void DetectSignalTransitions_ClearsWhenSignalStopsMatching()
    {
        var previous = new[] { new SignalSnapshot("MSFT", "RSI_D_OVERSOLD", "Daily RSI Oversold", true) };
        var current = new[] { new SignalSnapshot("MSFT", "RSI_D_OVERSOLD", "Daily RSI Oversold", false) };

        var transitions = AlertTransitionDetector.DetectSignalTransitions(current, previous, DefaultOptions);

        Assert.Single(transitions);
        Assert.Equal("SignalCleared", transitions[0].EventType);
    }

    [Fact]
    public void DetectScoreTransitions_FiresWhenScoreCrossesThreshold()
    {
        var previous = new[] { new ScoreSnapshot("NVDA", "DEEP_PULLBACK", 3) };
        var current = new[] { new ScoreSnapshot("NVDA", "DEEP_PULLBACK", 4.5m) };

        var transitions = AlertTransitionDetector.DetectScoreTransitions(current, previous, DefaultOptions);

        Assert.Single(transitions);
        Assert.Equal("ScoreThresholdCrossed", transitions[0].EventType);
        Assert.Equal("NVDA:DEEP_PULLBACK", transitions[0].SubjectKey);
    }

    [Fact]
    public void DetectScoreTransitions_IgnoresOtherProfileCodes()
    {
        var previous = new[]
        {
            new ScoreSnapshot("NVDA", "DEEP_PULLBACK", 3),
            new ScoreSnapshot("NVDA", "CHEAP_VS_MA", 0)
        };
        var current = new[]
        {
            new ScoreSnapshot("NVDA", "DEEP_PULLBACK", 3),
            new ScoreSnapshot("NVDA", "CHEAP_VS_MA", 5)
        };

        var transitions = AlertTransitionDetector.DetectScoreTransitions(current, previous, DefaultOptions);

        Assert.Empty(transitions);
    }

    [Fact]
    public void DetectProfileTransitions_FiresWhenScoreBecomesPositive()
    {
        var previous = new[] { new ScoreSnapshot("AAPL", "CHEAP_VS_MA", 0) };
        var current = new[] { new ScoreSnapshot("AAPL", "CHEAP_VS_MA", 2) };

        var transitions = AlertTransitionDetector.DetectProfileTransitions(current, previous, DefaultOptions);

        Assert.Single(transitions);
        Assert.Equal("ProfileEntered", transitions[0].EventType);
        Assert.Equal("Profile", transitions[0].SubjectType);
        Assert.Equal("AAPL:CHEAP_VS_MA", transitions[0].SubjectKey);
    }

    [Fact]
    public void DetectProfileTransitions_ClearsWhenScoreReturnsToZero()
    {
        var previous = new[] { new ScoreSnapshot("MSFT", "VOLUME_CONFIRMED_CHEAP", 3) };
        var current = new[] { new ScoreSnapshot("MSFT", "VOLUME_CONFIRMED_CHEAP", 0) };

        var transitions = AlertTransitionDetector.DetectProfileTransitions(current, previous, DefaultOptions);

        Assert.Single(transitions);
        Assert.Equal("ProfileExited", transitions[0].EventType);
    }

    [Fact]
    public void DetectProfileTransitions_RespectsProfileCodeFilter()
    {
        var options = new AlertsOptions
        {
            NotifyOnProfileEntered = true,
            ProfileCodes = ["DEEP_PULLBACK"]
        };
        var previous = new[]
        {
            new ScoreSnapshot("AAPL", "DEEP_PULLBACK", 0),
            new ScoreSnapshot("AAPL", "CHEAP_VS_MA", 0)
        };
        var current = new[]
        {
            new ScoreSnapshot("AAPL", "DEEP_PULLBACK", 2),
            new ScoreSnapshot("AAPL", "CHEAP_VS_MA", 3)
        };

        var transitions = AlertTransitionDetector.DetectProfileTransitions(current, previous, options);

        Assert.Single(transitions);
        Assert.Equal("AAPL:DEEP_PULLBACK", transitions[0].SubjectKey);
    }

    [Fact]
    public void ParseSymbols_SplitsAndDedupes()
    {
        var symbols = ScreenerQuery.ParseSymbols("aapl, MSFT; nvda  aapl");

        Assert.Equal(["AAPL", "MSFT", "NVDA"], symbols);
        Assert.Empty(ScreenerQuery.ParseSymbols(""));
    }

    [Fact]
    public void DetectBreadthTransitions_FiresWhenMetricCrossesBelowThreshold()
    {
        var previous = new[] { new BreadthSnapshotView("BMSB_W_ABOVE_PCT", 25, 250, 1000) };
        var current = new[] { new BreadthSnapshotView("BMSB_W_ABOVE_PCT", 18, 180, 1000) };

        var transitions = AlertTransitionDetector.DetectBreadthTransitions(current, previous, DefaultOptions);

        Assert.Single(transitions);
        Assert.Equal("BreadthThresholdCrossed", transitions[0].EventType);
        Assert.Equal("Market", transitions[0].SubjectType);
    }

    [Fact]
    public void DetectBreadthTransitions_FiresWhenMetricCrossesAboveThreshold()
    {
        var previous = new[] { new BreadthSnapshotView("BMSB_W_ABOVE_PCT", 45, 450, 1000) };
        var current = new[] { new BreadthSnapshotView("BMSB_W_ABOVE_PCT", 52, 520, 1000) };

        var transitions = AlertTransitionDetector.DetectBreadthTransitions(current, previous, DefaultOptions);

        Assert.Single(transitions);
        Assert.Contains("above", transitions[0].Message, StringComparison.OrdinalIgnoreCase);
    }
}
