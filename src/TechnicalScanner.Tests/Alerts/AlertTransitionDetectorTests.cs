using TechnicalScanner.Application.Alerts;
using TechnicalScanner.Application.Options;

namespace TechnicalScanner.Tests.Alerts;

public sealed class AlertTransitionDetectorTests
{
    private static readonly AlertsOptions DefaultOptions = new()
    {
        NotifyOnSignalFired = true,
        NotifyOnSignalCleared = true,
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
