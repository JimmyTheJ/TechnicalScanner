using TechnicalScanner.Application.Abstractions;

namespace TechnicalScanner.Application.Indicators;

public sealed class BenchmarkPriceLookup(ITechnicalScannerDbContext dbContext) : IBenchmarkPriceLookup
{
    private readonly Dictionary<(string Symbol, string Timeframe), IReadOnlyList<(DateOnly Date, decimal Close)>> _cache = [];

    public IReadOnlyList<(DateOnly Date, decimal Close)> GetCloses(string symbol, string timeframe)
    {
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        var timeframeCode = timeframe.Trim().ToUpperInvariant();
        var key = (normalizedSymbol, timeframeCode);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var tickerId = dbContext.Tickers
            .Where(t => t.IsActive && t.Symbol.ToUpper() == normalizedSymbol)
            .Select(t => (long?)t.Id)
            .FirstOrDefault();
        if (tickerId is null or 0)
        {
            _cache[key] = [];
            return [];
        }

        var closes = dbContext.PriceCandles
            .Where(p => p.TickerId == tickerId && p.Timeframe == timeframeCode)
            .OrderBy(p => p.CandleDate)
            .Select(p => new { p.CandleDate, Close = p.AdjustedClose ?? p.Close })
            .AsEnumerable()
            .Select(p => (p.CandleDate, p.Close))
            .ToList();

        _cache[key] = closes;
        return closes;
    }
}
