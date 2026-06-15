namespace TechnicalScanner.Domain.Enums;

public enum Timeframe
{
    D,
    W,
    M
}

public static class TimeframeExtensions
{
    public static string ToCode(this Timeframe timeframe) => timeframe switch
    {
        Timeframe.D => "D",
        Timeframe.W => "W",
        Timeframe.M => "M",
        _ => throw new ArgumentOutOfRangeException(nameof(timeframe))
    };

    public static Timeframe FromCode(string code) => code.ToUpperInvariant() switch
    {
        "D" => Timeframe.D,
        "W" => Timeframe.W,
        "M" => Timeframe.M,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unsupported timeframe code.")
    };
}
