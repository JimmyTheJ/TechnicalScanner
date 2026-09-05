namespace TechnicalScanner.Application.Abstractions;

public sealed class MarketDataRateLimitedException : Exception
{
    public string Operation { get; }

    public int ConsecutiveFailures { get; }

    public MarketDataRateLimitedException(string operation, int consecutiveFailures, Exception innerException)
        : base(
            $"Market data rate limited during {operation} after {consecutiveFailures} consecutive 429 responses.",
            innerException)
    {
        Operation = operation;
        ConsecutiveFailures = consecutiveFailures;
    }
}
