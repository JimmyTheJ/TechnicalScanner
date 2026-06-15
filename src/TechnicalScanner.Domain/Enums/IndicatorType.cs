namespace TechnicalScanner.Domain.Enums;

public enum IndicatorType
{
    BMSB,
    RSI,
    SMA,
    EMA,
    BOLLINGER_BANDS
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
        _ => throw new ArgumentOutOfRangeException(nameof(indicatorType))
    };

    public static IndicatorType FromCode(string code) => code.ToUpperInvariant() switch
    {
        "BMSB" => IndicatorType.BMSB,
        "RSI" => IndicatorType.RSI,
        "SMA" => IndicatorType.SMA,
        "EMA" => IndicatorType.EMA,
        "BOLLINGER_BANDS" => IndicatorType.BOLLINGER_BANDS,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unsupported indicator type.")
    };
}
