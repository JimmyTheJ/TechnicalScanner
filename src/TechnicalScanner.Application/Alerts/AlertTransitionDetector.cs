using System.Text.Json;
using TechnicalScanner.Application.Models;
using TechnicalScanner.Application.Options;

namespace TechnicalScanner.Application.Alerts;

public static class AlertTransitionDetector
{
    public static IReadOnlyList<AlertTransition> DetectSignalTransitions(
        IReadOnlyList<SignalSnapshot> current,
        IReadOnlyList<SignalSnapshot> previous,
        AlertsOptions options)
    {
        if (!options.NotifyOnSignalFired && !options.NotifyOnSignalCleared)
        {
            return [];
        }

        var allowedCodes = options.SignalCodes.Count == 0
            ? null
            : options.SignalCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var previousByKey = previous.ToDictionary(x => (x.TickerSymbol, x.SignalCode));
        var transitions = new List<AlertTransition>();

        foreach (var currentResult in current)
        {
            if (allowedCodes is not null && !allowedCodes.Contains(currentResult.SignalCode))
            {
                continue;
            }

            previousByKey.TryGetValue((currentResult.TickerSymbol, currentResult.SignalCode), out var prior);
            var wasMatch = prior?.IsMatch ?? false;
            var isMatch = currentResult.IsMatch;

            if (options.NotifyOnSignalFired && !wasMatch && isMatch)
            {
                transitions.Add(new AlertTransition(
                    "SignalFired",
                    "Ticker",
                    currentResult.TickerSymbol,
                    $"{currentResult.TickerSymbol} — {currentResult.SignalName}",
                    $"{currentResult.TickerSymbol} fired signal {currentResult.SignalCode}: {currentResult.SignalName}.",
                    JsonSerializer.Serialize(new
                    {
                        currentResult.TickerSymbol,
                        currentResult.SignalCode,
                        currentResult.SignalName,
                        currentResult.IsMatch
                    })));
            }
            else if (options.NotifyOnSignalCleared && wasMatch && !isMatch)
            {
                transitions.Add(new AlertTransition(
                    "SignalCleared",
                    "Ticker",
                    currentResult.TickerSymbol,
                    $"{currentResult.TickerSymbol} — {currentResult.SignalName} cleared",
                    $"{currentResult.TickerSymbol} cleared signal {currentResult.SignalCode}: {currentResult.SignalName}.",
                    JsonSerializer.Serialize(new
                    {
                        currentResult.TickerSymbol,
                        currentResult.SignalCode,
                        currentResult.SignalName,
                        currentResult.IsMatch
                    })));
            }
        }

        return transitions;
    }

    public static IReadOnlyList<AlertTransition> DetectScoreTransitions(
        IReadOnlyList<ScoreSnapshot> current,
        IReadOnlyList<ScoreSnapshot> previous,
        AlertsOptions options)
    {
        var transitions = new List<AlertTransition>();

        var previousByKey = previous.ToDictionary(
            x => SnapshotKey(x.TickerSymbol, x.ScoreCode),
            StringComparer.OrdinalIgnoreCase);

        foreach (var currentScore in current.Where(x =>
                     x.ScoreCode.Equals(options.CompositeScoreCode, StringComparison.OrdinalIgnoreCase)))
        {
            previousByKey.TryGetValue(SnapshotKey(currentScore.TickerSymbol, currentScore.ScoreCode), out var prior);
            var priorScore = prior?.Score ?? 0m;
            if (priorScore < options.CompositeScoreThreshold && currentScore.Score >= options.CompositeScoreThreshold)
            {
                transitions.Add(new AlertTransition(
                    "ScoreThresholdCrossed",
                    "Ticker",
                    SnapshotKey(currentScore.TickerSymbol, currentScore.ScoreCode),
                    $"{currentScore.TickerSymbol} {currentScore.ScoreCode} crossed {options.CompositeScoreThreshold}",
                    $"{currentScore.TickerSymbol} {currentScore.ScoreCode} score rose to {currentScore.Score:0.##} (threshold {options.CompositeScoreThreshold:0.##}).",
                    JsonSerializer.Serialize(new
                    {
                        currentScore.TickerSymbol,
                        currentScore.ScoreCode,
                        currentScore.Score,
                        options.CompositeScoreThreshold
                    })));
            }
        }

        return transitions;
    }

    public static IReadOnlyList<AlertTransition> DetectProfileTransitions(
        IReadOnlyList<ScoreSnapshot> current,
        IReadOnlyList<ScoreSnapshot> previous,
        AlertsOptions options)
    {
        if (!options.NotifyOnProfileEntered && !options.NotifyOnProfileExited)
        {
            return [];
        }

        var allowedCodes = options.ProfileCodes.Count == 0
            ? null
            : options.ProfileCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var previousByKey = previous.ToDictionary(
            x => SnapshotKey(x.TickerSymbol, x.ScoreCode),
            StringComparer.OrdinalIgnoreCase);
        var transitions = new List<AlertTransition>();

        foreach (var currentScore in current)
        {
            if (allowedCodes is not null && !allowedCodes.Contains(currentScore.ScoreCode))
            {
                continue;
            }

            previousByKey.TryGetValue(SnapshotKey(currentScore.TickerSymbol, currentScore.ScoreCode), out var prior);
            var wasIn = (prior?.Score ?? 0m) > 0m;
            var isIn = currentScore.Score > 0m;

            if (options.NotifyOnProfileEntered && !wasIn && isIn)
            {
                transitions.Add(new AlertTransition(
                    "ProfileEntered",
                    "Profile",
                    SnapshotKey(currentScore.TickerSymbol, currentScore.ScoreCode),
                    $"{currentScore.TickerSymbol} entered {currentScore.ScoreCode}",
                    $"{currentScore.TickerSymbol} now matches cheapness profile {currentScore.ScoreCode} (score {currentScore.Score:0.##}).",
                    JsonSerializer.Serialize(new
                    {
                        currentScore.TickerSymbol,
                        ProfileCode = currentScore.ScoreCode,
                        currentScore.Score
                    })));
            }
            else if (options.NotifyOnProfileExited && wasIn && !isIn)
            {
                transitions.Add(new AlertTransition(
                    "ProfileExited",
                    "Profile",
                    SnapshotKey(currentScore.TickerSymbol, currentScore.ScoreCode),
                    $"{currentScore.TickerSymbol} left {currentScore.ScoreCode}",
                    $"{currentScore.TickerSymbol} no longer matches cheapness profile {currentScore.ScoreCode}.",
                    JsonSerializer.Serialize(new
                    {
                        currentScore.TickerSymbol,
                        ProfileCode = currentScore.ScoreCode,
                        currentScore.Score
                    })));
            }
        }

        return transitions;
    }

    private static string SnapshotKey(string tickerSymbol, string scoreCode)
        => $"{tickerSymbol}:{scoreCode}";

    public static IReadOnlyList<AlertTransition> DetectBreadthTransitions(
        IReadOnlyList<BreadthSnapshotView> current,
        IReadOnlyList<BreadthSnapshotView> previous,
        AlertsOptions options)
    {
        if (options.BreadthRules.Count == 0)
        {
            return [];
        }

        var previousByMetric = previous.ToDictionary(x => x.MetricCode, StringComparer.OrdinalIgnoreCase);
        var transitions = new List<AlertTransition>();

        foreach (var rule in options.BreadthRules)
        {
            var currentMetric = current.FirstOrDefault(x =>
                x.MetricCode.Equals(rule.MetricCode, StringComparison.OrdinalIgnoreCase));
            if (currentMetric is null)
            {
                continue;
            }

            previousByMetric.TryGetValue(rule.MetricCode, out var priorMetric);
            var priorPct = priorMetric?.MatchPct ?? currentMetric.MatchPct;
            var currentPct = currentMetric.MatchPct;

            var crossedBelow = priorPct >= rule.Threshold && currentPct < rule.Threshold;
            var crossedAbove = priorPct < rule.Threshold && currentPct >= rule.Threshold;

            if (rule.Direction.Equals("CrossBelow", StringComparison.OrdinalIgnoreCase) && crossedBelow)
            {
                transitions.Add(CreateBreadthTransition(rule, currentMetric, currentPct, "below"));
            }
            else if (rule.Direction.Equals("CrossAbove", StringComparison.OrdinalIgnoreCase) && crossedAbove)
            {
                transitions.Add(CreateBreadthTransition(rule, currentMetric, currentPct, "above"));
            }
        }

        return transitions;
    }

    private static AlertTransition CreateBreadthTransition(
        BreadthAlertRuleOptions rule,
        BreadthSnapshotView currentMetric,
        decimal currentPct,
        string direction)
        => new(
            "BreadthThresholdCrossed",
            "Market",
            currentMetric.MetricCode,
            $"Market breadth {currentMetric.MetricCode} crossed {direction} {rule.Threshold}%",
            $"{currentMetric.MetricCode} breadth moved {direction} {rule.Threshold:0.##}% to {currentPct:0.##}% ({currentMetric.MatchCount}/{currentMetric.TotalValidStocks} stocks).",
            JsonSerializer.Serialize(new
            {
                rule.MetricCode,
                rule.Threshold,
                rule.Direction,
                currentPct,
                currentMetric.MatchCount,
                currentMetric.TotalValidStocks
            }));
}

public sealed record SignalSnapshot(
    string TickerSymbol,
    string SignalCode,
    string SignalName,
    bool IsMatch);

public sealed record ScoreSnapshot(
    string TickerSymbol,
    string ScoreCode,
    decimal Score);

public sealed record BreadthSnapshotView(
    string MetricCode,
    decimal MatchPct,
    int MatchCount,
    int TotalValidStocks);
