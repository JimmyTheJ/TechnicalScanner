using System.Text.Json;
using TechnicalScanner.Application.Signals;

namespace TechnicalScanner.Application.Confluence;

public sealed record ConfluenceCheck(
    string Label,
    string Timeframe,
    string IndicatorCode,
    string Field,
    string Operator,
    JsonElement Value,
    string Side);

public sealed record ConfluenceComponent(
    string Label,
    string Timeframe,
    string IndicatorCode,
    string Field,
    string Operator,
    string Side,
    bool Available,
    bool Matched);

public sealed record ConfluenceTally(
    int CheapHits,
    int CheapValid,
    decimal CheapPct,
    int ExtendedHits,
    int ExtendedValid,
    decimal ExtendedPct,
    string Bias,
    IReadOnlyList<ConfluenceComponent> Components);

public static class ConfluenceCatalog
{
    public const string Cheap = "Cheap";
    public const string Extended = "Extended";

    public static IReadOnlyList<ConfluenceCheck> Checks { get; } =
    [
        Check("Daily RSI oversold", "D", "RSI_14_D", "rsi", "<", 30m, Cheap),
        Check("Weekly RSI weak", "W", "RSI_14_W", "rsi", "<", 40m, Cheap),
        Check("Monthly RSI weak", "M", "RSI_14_M", "rsi", "<", 50m, Cheap),
        Check("Below weekly BMSB", "W", "BMSB_W", "status", "=", "BelowBand", Cheap),
        Check("Below 200D SMA", "D", "SMA_200_D", "distanceFromSmaPct", "<", 0m, Cheap),
        Check("Below 50W SMA", "W", "SMA_50_W", "distanceFromSmaPct", "<", 0m, Cheap),
        Check("Below lower Bollinger", "D", "BB_20_2_D", "percentB", "<", 0m, Cheap),
        Check("Negative 21D ROC", "D", "ROC_21_D", "rocPct", "<", 0m, Cheap),
        Check("20%+ below ATH", "D", "DRAWDOWN_ATH_D", "distanceFromAthPct", "<", -20m, Cheap),
        Check("Daily RSI overbought", "D", "RSI_14_D", "rsi", ">", 70m, Extended),
        Check("Weekly RSI strong", "W", "RSI_14_W", "rsi", ">", 60m, Extended),
        Check("Monthly RSI strong", "M", "RSI_14_M", "rsi", ">", 60m, Extended),
        Check("Above weekly BMSB", "W", "BMSB_W", "status", "=", "AboveBand", Extended),
        Check("Above 200D SMA", "D", "SMA_200_D", "distanceFromSmaPct", ">", 0m, Extended),
        Check("Above 50W SMA", "W", "SMA_50_W", "distanceFromSmaPct", ">", 0m, Extended),
        Check("Above upper Bollinger", "D", "BB_20_2_D", "percentB", ">", 1m, Extended)
    ];

    public static IReadOnlyList<string> IndicatorCodes { get; } =
        Checks.Select(check => check.IndicatorCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static ConfluenceCheck Check(
        string label,
        string timeframe,
        string indicatorCode,
        string field,
        string op,
        object value,
        string side)
        => new(
            label,
            timeframe,
            indicatorCode,
            field,
            op,
            JsonSerializer.SerializeToElement(value),
            side);
}

public static class ConfluenceScoring
{
    public static ConfluenceTally Evaluate(
        IReadOnlyDictionary<string, Dictionary<string, JsonElement>> values,
        SignalRuleEvaluator evaluator)
    {
        var components = new List<ConfluenceComponent>(ConfluenceCatalog.Checks.Count);
        var cheapHits = 0;
        var cheapValid = 0;
        var extendedHits = 0;
        var extendedValid = 0;

        foreach (var check in ConfluenceCatalog.Checks)
        {
            var available = values.ContainsKey(check.IndicatorCode);
            var matched = available && evaluator.Evaluate(ToRule(check), values);
            if (available)
            {
                if (check.Side == ConfluenceCatalog.Cheap)
                {
                    cheapValid++;
                    if (matched)
                    {
                        cheapHits++;
                    }
                }
                else
                {
                    extendedValid++;
                    if (matched)
                    {
                        extendedHits++;
                    }
                }
            }

            components.Add(new ConfluenceComponent(
                check.Label,
                check.Timeframe,
                check.IndicatorCode,
                check.Field,
                check.Operator,
                check.Side,
                available,
                matched));
        }

        return new ConfluenceTally(
            cheapHits,
            cheapValid,
            Pct(cheapHits, cheapValid),
            extendedHits,
            extendedValid,
            Pct(extendedHits, extendedValid),
            Bias(cheapHits, extendedHits),
            components);
    }

    private static string ToRule(ConfluenceCheck check)
        => JsonSerializer.Serialize(new
        {
            indicatorCode = check.IndicatorCode,
            field = check.Field,
            @operator = check.Operator,
            value = check.Value
        });

    private static decimal Pct(int hits, int valid)
        => valid == 0 ? 0m : Math.Round((decimal)hits / valid * 100m, 1);

    private static string Bias(int cheapHits, int extendedHits)
    {
        if (cheapHits > extendedHits)
        {
            return ConfluenceCatalog.Cheap;
        }

        if (extendedHits > cheapHits)
        {
            return ConfluenceCatalog.Extended;
        }

        return "Neutral";
    }
}
