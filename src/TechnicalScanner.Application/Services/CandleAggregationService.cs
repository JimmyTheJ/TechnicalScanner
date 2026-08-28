using Microsoft.Extensions.Logging;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Domain.Entities;
using TechnicalScanner.Domain.Enums;

namespace TechnicalScanner.Application.Services;

public sealed class CandleAggregationService(
    ITechnicalScannerDbContext dbContext,
    ILogger<CandleAggregationService> logger)
{
    public async Task<int> AggregateAsync(IEnumerable<Timeframe> targetTimeframes, CancellationToken cancellationToken)
    {
        var total = 0;
        var tickers = dbContext.Tickers.Where(t => t.IsActive).ToList();

        foreach (var ticker in tickers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var daily = dbContext.PriceCandles
                .Where(p => p.TickerId == ticker.Id && p.Timeframe == Timeframe.D.ToCode())
                .OrderBy(p => p.CandleDate)
                .ToList();

            foreach (var timeframe in targetTimeframes)
            {
                total += timeframe switch
                {
                    Timeframe.W => await AggregateWeeklyAsync(ticker, daily, cancellationToken),
                    Timeframe.M => await AggregateMonthlyAsync(ticker, daily, cancellationToken),
                    _ => 0
                };
            }
        }

        logger.LogInformation("Aggregated {Count} higher-timeframe candles.", total);
        return total;
    }

    private async Task<int> AggregateWeeklyAsync(Ticker ticker, IReadOnlyList<PriceCandle> daily, CancellationToken cancellationToken)
    {
        var grouped = daily
            .GroupBy(c => GetWeekStart(c.CandleDate))
            .OrderBy(g => g.Key)
            .Select(g => (Date: g.Key, Candles: g.OrderBy(c => c.CandleDate).ToList()))
            .Where(g => g.Candles.Count > 0)
            .ToList();

        return await UpsertAggregatedCandlesAsync(ticker.Id, Timeframe.W, grouped, cancellationToken);
    }

    private async Task<int> AggregateMonthlyAsync(Ticker ticker, IReadOnlyList<PriceCandle> daily, CancellationToken cancellationToken)
    {
        var grouped = daily
            .GroupBy(c => new DateOnly(c.CandleDate.Year, c.CandleDate.Month, 1))
            .OrderBy(g => g.Key)
            .Select(g => (Date: g.Key, Candles: g.OrderBy(c => c.CandleDate).ToList()))
            .Where(g => g.Candles.Count > 0)
            .ToList();

        return await UpsertAggregatedCandlesAsync(ticker.Id, Timeframe.M, grouped, cancellationToken);
    }

    private async Task<int> UpsertAggregatedCandlesAsync(
        long tickerId,
        Timeframe timeframe,
        IReadOnlyList<(DateOnly Date, List<PriceCandle> Candles)> groups,
        CancellationToken cancellationToken)
    {
        if (groups.Count == 0)
        {
            return 0;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var timeframeCode = timeframe.ToCode();
        var dates = groups.Select(g => g.Date).ToList();
        var existing = dbContext.PriceCandles
            .Where(p => p.TickerId == tickerId && p.Timeframe == timeframeCode && dates.Contains(p.CandleDate))
            .ToList()
            .ToDictionary(p => p.CandleDate);

        foreach (var (candleDate, sourceCandles) in groups)
        {
            var open = sourceCandles.First().Open ?? sourceCandles.First().Close;
            var close = sourceCandles.Last().AdjustedClose ?? sourceCandles.Last().Close;
            var high = sourceCandles.Max(c => c.High ?? c.Close);
            var low = sourceCandles.Min(c => c.Low ?? c.Close);
            var volume = sourceCandles.Sum(c => c.Volume ?? 0);

            if (existing.TryGetValue(candleDate, out var row))
            {
                row.Open = open;
                row.High = high;
                row.Low = low;
                row.Close = close;
                row.AdjustedClose = close;
                row.Volume = volume;
                row.Source = "aggregated";
                row.UpdatedUtc = DateTimeOffset.UtcNow;
            }
            else
            {
                dbContext.Add(new PriceCandle
                {
                    TickerId = tickerId,
                    Timeframe = timeframeCode,
                    CandleDate = candleDate,
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    AdjustedClose = close,
                    Volume = volume,
                    Source = "aggregated"
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return groups.Count;
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var offset = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        return date.AddDays(-offset);
    }
}
