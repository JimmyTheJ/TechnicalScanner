namespace TechnicalScanner.Application.Abstractions;

public interface IBenchmarkPriceLookup
{
    IReadOnlyList<(DateOnly Date, decimal Close)> GetCloses(string symbol, string timeframe);
}
