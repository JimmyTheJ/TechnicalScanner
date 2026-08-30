namespace TechnicalScanner.Application.Options;

public sealed class MarketDataOptions
{
    public const string SectionName = "MarketData";

    public string Provider { get; set; } = "YahooFinance";

    public double RequestsPerSecond { get; set; } = 2;

    public int MaxRetries { get; set; } = 3;

    public int RetryDelaySeconds { get; set; } = 2;

    public int TooManyRequestsDelayMinutes { get; set; } = 30;

    public int InitialHistoryYears { get; set; } = 10;

    public UniverseOptions Universe { get; set; } = new();
}

public sealed class UniverseOptions
{
    public bool IncludeUs { get; set; } = true;

    public bool IncludeCanada { get; set; } = true;

    public bool IncludeEtfs { get; set; } = true;
}
