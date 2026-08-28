namespace TechnicalScanner.Domain.Enums;

public enum IndicatorType
{
    BMSB,
    RSI,
    SMA,
    EMA,
    BOLLINGER_BANDS,
    VOLUME,
    ATR,
    PRICE_RANGE
}

public static class IndicatorTypeExtensions
{
    public static string ToCode(this IndicatorType indicatorType) => indicatorType switch
    {
        IndicatorType.BMSB => "BMSB",
        IndicatorType.RSI => "RSI",
        IndicatorType.SMA => "SMA",
        IndicatorType.EMA => "EMA",
        IndicatorType.BOLLINGER_BANDS => "BOLLINGER_BANDS",
        IndicatorType.VOLUME => "VOLUME",
        IndicatorType.ATR => "ATR",
        IndicatorType.PRICE_RANGE => "PRICE_RANGE",
        _ => throw new ArgumentOutOfRangeException(nameof(indicatorType))
    };

    public static IndicatorType FromCode(string code) => code.ToUpperInvariant() switch
    {
        "BMSB" => IndicatorType.BMSB,
        "RSI" => IndicatorType.RSI,
        "SMA" => IndicatorType.SMA,
        "EMA" => IndicatorType.EMA,
        "BOLLINGER_BANDS" => IndicatorType.BOLLINGER_BANDS,
        "VOLUME" => IndicatorType.VOLUME,
        "ATR" => IndicatorType.ATR,
        "PRICE_RANGE" => IndicatorType.PRICE_RANGE,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unsupported indicator type.")
    };
}
