using System.Text.Json;
using TechnicalScanner.Application.Signals;

namespace TechnicalScanner.Application.Profiles;

public readonly record struct BreadthTally(
    int Valid,
    int Matches,
    decimal MatchPct,
    decimal? Median,
    decimal? Average);

public static class BreadthAggregation
{
    public const string AllUniverse = "ALL";
    public const string AllSlices = "*";

    private static readonly Dictionary<string, Dictionary<string, JsonElement>> Empty =
        new(StringComparer.OrdinalIgnoreCase);

    public static string ProfileMetricCode(string profileCode)
        => $"PROFILE_{profileCode.Trim().ToUpperInvariant()}_PCT";

    public static bool IsProfileMetric(string metricCode)
        => metricCode.StartsWith("PROFILE_", StringComparison.OrdinalIgnoreCase)
           && metricCode.EndsWith("_PCT", StringComparison.OrdinalIgnoreCase);

    public static string? ProfileCodeFromMetric(string metricCode)
        => IsProfileMetric(metricCode) ? metricCode[8..^4] : null;

    public static BreadthTally Tally(int valid, int matches, IReadOnlyList<decimal> values)
    {
        var pct = valid == 0 ? 0m : Math.Round((decimal)matches / valid * 100m, 2);
        return new BreadthTally(
            valid,
            matches,
            pct,
            ProfileBacktestStats.Median(values),
            ProfileBacktestStats.Mean(values));
    }

    public static bool UsesScoreFallback(string ruleJson, SignalRuleEvaluator evaluator)
    {
        if (string.IsNullOrWhiteSpace(ruleJson))
        {
            return true;
        }

        try
        {
            return SignalRuleEvaluator.CollectIndicatorCodes(ruleJson).Count == 0
                && evaluator.Evaluate(ruleJson, Empty, Empty);
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }
}
