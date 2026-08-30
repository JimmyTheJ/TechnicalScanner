using System.Text.Json;

namespace TechnicalScanner.Application.Indicators;

public sealed record IndicatorParameterSchema(string Name, string ValueType, object? DefaultValue, string Description);

public sealed record IndicatorFieldSchema(string Name, string ValueType, string Description);

public sealed record IndicatorTypeCatalogEntry(
    string IndicatorType,
    string Name,
    string Description,
    string DefaultCodePattern,
    IReadOnlyList<IndicatorParameterSchema> Parameters,
    IReadOnlyList<IndicatorFieldSchema> Fields);

public static class IndicatorTypeCatalog
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<IndicatorTypeCatalogEntry> Entries { get; } =
    [
        new(
            "SMA",
            "Simple Moving Average",
            "Close versus an SMA of the selected period and timeframe.",
            "SMA_{period}_{timeframe}",
            [new("period", "number", 50, "Lookback length in bars")],
            [
                new("distanceFromSmaPct", "number", "Percent distance from the SMA"),
                new("closeAboveSma", "boolean", "Close is above the SMA"),
                new("closeBelowSma", "boolean", "Close is below the SMA"),
                new("smaSlopePositive", "boolean", "SMA is rising versus the prior bar"),
                new("smaSlopeNegative", "boolean", "SMA is falling versus the prior bar"),
                new("sma", "number", "SMA value"),
                new("close", "number", "Close used in the calculation")
            ]),
        new(
            "EMA",
            "Exponential Moving Average",
            "Close versus an EMA of the selected period and timeframe.",
            "EMA_{period}_{timeframe}",
            [new("period", "number", 21, "Lookback length in bars")],
            [
                new("distanceFromEmaPct", "number", "Percent distance from the EMA"),
                new("closeAboveEma", "boolean", "Close is above the EMA"),
                new("closeBelowEma", "boolean", "Close is below the EMA"),
                new("emaSlopePositive", "boolean", "EMA is rising versus the prior bar"),
                new("emaSlopeNegative", "boolean", "EMA is falling versus the prior bar"),
                new("ema", "number", "EMA value"),
                new("close", "number", "Close used in the calculation")
            ]),
        new(
            "RSI",
            "Relative Strength Index",
            "Wilder RSI with oversold/overbought categories.",
            "RSI_{period}_{timeframe}",
            [new("period", "number", 14, "RSI lookback")],
            [
                new("rsi", "number", "RSI value 0-100"),
                new("category", "string", "Oversold, Weak, Neutral, Strong, Overbought, ExtremelyOverbought"),
                new("close", "number", "Close used in the calculation")
            ]),
        new(
            "BOLLINGER_BANDS",
            "Bollinger Bands",
            "Percent B and bandwidth around an SMA envelope.",
            "BB_{period}_{stdDev}_{timeframe}",
            [
                new("period", "number", 20, "SMA lookback"),
                new("stdDevMultiplier", "number", 2, "Standard deviation multiplier")
            ],
            [
                new("percentB", "number", "Position inside the bands (0 = lower, 1 = upper)"),
                new("bandWidthPct", "number", "Band width as percent of the middle band"),
                new("status", "string", "CloseBelowLowerBand, CloseNearLowerBand, InsideBands, CloseNearUpperBand, CloseAboveUpperBand"),
                new("close", "number", "Close used in the calculation")
            ]),
        new(
            "BMSB",
            "Bull Market Support Band",
            "Weekly close versus the 20 SMA / 21 EMA band.",
            "BMSB_{timeframe}",
            [
                new("smaPeriod", "number", 20, "SMA period"),
                new("emaPeriod", "number", 21, "EMA period")
            ],
            [
                new("status", "string", "BelowBand, InsideBand, AboveBand"),
                new("distanceFromBandMidpointPct", "number", "Percent distance from band midpoint"),
                new("distanceFromBandBottomPct", "number", "Percent distance from band bottom"),
                new("distanceFromBandTopPct", "number", "Percent distance from band top"),
                new("close", "number", "Close used in the calculation")
            ]),
        new(
            "VOLUME",
            "Volume SMA / relative volume",
            "Current volume versus a volume moving average, plus dollar volume.",
            "VOL_SMA_{period}_{timeframe}",
            [new("period", "number", 20, "Volume SMA lookback")],
            [
                new("relativeVolume", "number", "Volume / volume SMA"),
                new("volumeSma", "number", "Volume moving average"),
                new("volume", "number", "Bar volume"),
                new("dollarVolume", "number", "Close times volume"),
                new("close", "number", "Close used in the calculation")
            ]),
        new(
            "ATR",
            "Average True Range",
            "Wilder ATR as a percent of close (volatility).",
            "ATR_{period}_{timeframe}",
            [new("period", "number", 14, "ATR lookback")],
            [
                new("atrPct", "number", "ATR as percent of close"),
                new("atr", "number", "ATR in price units"),
                new("trueRange", "number", "True range of this bar"),
                new("close", "number", "Close used in the calculation")
            ]),
        new(
            "PRICE_RANGE",
            "N-bar high / low range",
            "Distance from the highest high and lowest low over a lookback window.",
            "RANGE_{period}_{timeframe}",
            [new("period", "number", 252, "Lookback in bars (252 ≈ 52 weeks on daily)")],
            [
                new("distanceFromHighPct", "number", "Percent distance from the window high"),
                new("distanceFromLowPct", "number", "Percent distance from the window low"),
                new("rangePosition", "number", "0 at the low, 1 at the high"),
                new("periodHigh", "number", "Highest high in the window"),
                new("periodLow", "number", "Lowest low in the window"),
                new("close", "number", "Close used in the calculation")
            ]),
        new(
            "ROC",
            "Rate of change",
            "Percent change versus the close N bars ago.",
            "ROC_{period}_{timeframe}",
            [new("period", "number", 21, "Lookback in bars")],
            [
                new("rocPct", "number", "Percent change versus the close N bars ago"),
                new("priorClose", "number", "Close N bars ago"),
                new("close", "number", "Close used in the calculation")
            ]),
        new(
            "RELATIVE_STRENGTH",
            "Relative strength vs benchmark",
            "Excess return versus a benchmark such as SPY, QQQ, or XIU.TO.",
            "RS_{benchmark}_{period}_{timeframe}",
            [
                new("period", "number", 63, "Lookback in bars"),
                new("benchmarkSymbol", "string", "SPY", "Benchmark ticker (SPY, QQQ, XIU.TO, …)")
            ],
            [
                new("relativeStrengthPct", "number", "Stock return minus benchmark return, in percent"),
                new("stockReturnPct", "number", "Stock percent return over the window"),
                new("benchmarkReturnPct", "number", "Benchmark percent return over the window"),
                new("benchmarkSymbol", "string", "Benchmark ticker used"),
                new("close", "number", "Close used in the calculation")
            ]),
        new(
            "DRAWDOWN",
            "Drawdown from all-time high",
            "Percent distance from the highest high in stored history for this timeframe.",
            "DRAWDOWN_ATH_{timeframe}",
            [],
            [
                new("distanceFromAthPct", "number", "Percent distance from the all-time high (negative when below)"),
                new("drawdownPct", "number", "Percent decline from the all-time high to close"),
                new("ath", "number", "Highest high seen so far"),
                new("close", "number", "Close used in the calculation")
            ])
    ];

    public static IndicatorTypeCatalogEntry? Find(string indicatorType)
        => Entries.FirstOrDefault(e => e.IndicatorType.Equals(indicatorType, StringComparison.OrdinalIgnoreCase));

    public static string SuggestCode(string indicatorType, string timeframe, JsonElement parameters)
    {
        var tf = timeframe.Trim().ToUpperInvariant();
        var type = indicatorType.Trim().ToUpperInvariant();
        var period = ReadInt(parameters, "period");
        return type switch
        {
            "SMA" => $"SMA_{period}_{tf}",
            "EMA" => $"EMA_{period}_{tf}",
            "RSI" => $"RSI_{period}_{tf}",
            "VOLUME" => $"VOL_SMA_{period}_{tf}",
            "ATR" => $"ATR_{period}_{tf}",
            "PRICE_RANGE" => $"RANGE_{period}_{tf}",
            "ROC" => $"ROC_{period}_{tf}",
            "RELATIVE_STRENGTH" => $"RS_{IndicatorMath.SanitizeCodeToken(ReadString(parameters, "benchmarkSymbol"))}_{period}_{tf}",
            "BOLLINGER_BANDS" => $"BB_{period}_{ReadNumber(parameters, "stdDevMultiplier")}_{tf}",
            "BMSB" => $"BMSB_{tf}",
            "DRAWDOWN" => $"DRAWDOWN_ATH_{tf}",
            _ => $"{type}_{period}_{tf}"
        };
    }

    public static string SuggestName(string indicatorType, string timeframe, JsonElement parameters)
    {
        var tfLabel = timeframe.Trim().ToUpperInvariant() switch
        {
            "D" => "Day",
            "W" => "Week",
            "M" => "Month",
            _ => timeframe
        };
        var period = ReadInt(parameters, "period");
        return indicatorType.Trim().ToUpperInvariant() switch
        {
            "SMA" => $"{period} {tfLabel} SMA",
            "EMA" => $"{period} {tfLabel} EMA",
            "RSI" => $"{tfLabel} RSI {period}",
            "VOLUME" => $"{period} {tfLabel} Volume SMA",
            "ATR" => $"{period} {tfLabel} ATR",
            "PRICE_RANGE" => $"{period} {tfLabel} High/Low Range",
            "ROC" => $"{period} {tfLabel} Rate of Change",
            "RELATIVE_STRENGTH" => $"{period} {tfLabel} vs {ReadString(parameters, "benchmarkSymbol")}",
            "BOLLINGER_BANDS" => $"{tfLabel} Bollinger Bands {period}/{ReadNumber(parameters, "stdDevMultiplier")}",
            "BMSB" => "Bull Market Support Band",
            "DRAWDOWN" => $"{tfLabel} drawdown from ATH",
            _ => $"{indicatorType} {period} {tfLabel}"
        };
    }

    private static int ReadInt(JsonElement parameters, string name)
        => parameters.ValueKind == JsonValueKind.Object &&
           parameters.TryGetProperty(name, out var value) &&
           value.TryGetInt32(out var parsed)
            ? parsed
            : 0;

    private static string ReadString(JsonElement parameters, string name)
    {
        if (parameters.ValueKind != JsonValueKind.Object || !parameters.TryGetProperty(name, out var value))
        {
            return "SPY";
        }

        return value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : "SPY";
    }

    private static string ReadNumber(JsonElement parameters, string name)
    {
        if (parameters.ValueKind != JsonValueKind.Object || !parameters.TryGetProperty(name, out var value))
        {
            return "0";
        }

        return value.ValueKind == JsonValueKind.Number ? value.ToString() : "0";
    }
}
