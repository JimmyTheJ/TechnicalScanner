using System.Text.Json;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Models;
using TechnicalScanner.Domain.Enums;

namespace TechnicalScanner.Infrastructure.MarketData;

/// <summary>
/// Seed market data provider for development and Phase 1 vertical slice.
/// Replace with a Yahoo Finance / Polygon provider for production use.
/// </summary>
public sealed class SeedMarketDataProvider : IMarketDataProvider
{
    public static readonly IReadOnlyList<TickerDto> DefaultTickers =
    [
        new("AAPL", "NASDAQ", "US", "USD", "Apple Inc.", "Technology", "Consumer Electronics", null, false),
        new("MSFT", "NASDAQ", "US", "USD", "Microsoft Corporation", "Technology", "Software", null, false),
        new("NVDA", "NASDAQ", "US", "USD", "NVIDIA Corporation", "Technology", "Semiconductors", null, false),
        new("TSLA", "NASDAQ", "US", "USD", "Tesla, Inc.", "Consumer Cyclical", "Auto Manufacturers", null, false),
        new("SPY", "NYSE", "US", "USD", "SPDR S&P 500 ETF Trust", null, null, null, true),
        new("QQQ", "NASDAQ", "US", "USD", "Invesco QQQ Trust", null, null, null, true),
        new("SHOP", "TSX", "CA", "CAD", "Shopify Inc.", "Technology", "Software", null, false),
        new("RY", "TSX", "CA", "CAD", "Royal Bank of Canada", "Financial Services", "Banks", null, false),
        new("TD", "TSX", "CA", "CAD", "Toronto-Dominion Bank", "Financial Services", "Banks", null, false),
        new("XIU", "TSX", "CA", "CAD", "iShares S&P/TSX 60 Index ETF", null, null, null, true)
    ];

    public Task<IReadOnlyList<TickerDto>> GetTickersAsync(CancellationToken cancellationToken)
        => Task.FromResult(DefaultTickers);

    public Task<IReadOnlyList<PriceCandleDto>> GetHistoricalCandlesAsync(
        string symbol,
        string exchange,
        string country,
        Timeframe timeframe,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        if (timeframe != Timeframe.D)
        {
            return Task.FromResult<IReadOnlyList<PriceCandleDto>>([]);
        }

        var candles = new List<PriceCandleDto>();
        var seed = HashCode.Combine(symbol, exchange, country);
        var random = new Random(seed);
        var price = 50m + (seed % 200);

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var drift = (decimal)(random.NextDouble() - 0.48) * 2m;
            var open = price;
            var close = Math.Max(1m, price + drift);
            var high = Math.Max(open, close) + (decimal)random.NextDouble();
            var low = Math.Min(open, close) - (decimal)random.NextDouble();
            price = close;

            candles.Add(new PriceCandleDto(
                date,
                open,
                high,
                low,
                close,
                close,
                random.Next(1_000_000, 50_000_000),
                "seed"));
        }

        return Task.FromResult<IReadOnlyList<PriceCandleDto>>(candles);
    }
}
