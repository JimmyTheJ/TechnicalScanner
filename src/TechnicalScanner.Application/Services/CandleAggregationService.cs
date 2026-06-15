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
            .OrderBy(g => g.Key);

        var count = 0;
        foreach (var group in grouped)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candles = group.OrderBy(c => c.CandleDate).ToList();
            if (candles.Count == 0)
            {
                continue;
            }

            await UpsertAggregatedCandleAsync(
                ticker.Id,
                Timeframe.W,
                group.Key,
                candles,
                cancellationToken);
            count++;
        }

        return count;
    }

    private async Task<int> AggregateMonthlyAsync(Ticker ticker, IReadOnlyList<PriceCandle> daily, CancellationToken cancellationToken)
    {
        var grouped = daily
            .GroupBy(c => new DateOnly(c.CandleDate.Year, c.CandleDate.Month, 1))
            .OrderBy(g => g.Key);

        var count = 0;
        foreach (var group in grouped)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candles = group.OrderBy(c => c.CandleDate).ToList();
            if (candles.Count == 0)
            {
                continue;
            }

            await UpsertAggregatedCandleAsync(
                ticker.Id,
                Timeframe.M,
                group.Key,
                candles,
                cancellationToken);
            count++;
        }

        return count;
    }

    private async Task UpsertAggregatedCandleAsync(
        long tickerId,
        Timeframe timeframe,
        DateOnly candleDate,
        IReadOnlyList<PriceCandle> sourceCandles,
        CancellationToken cancellationToken)
    {
        var timeframeCode = timeframe.ToCode();
        var existing = dbContext.PriceCandles.FirstOrDefault(p =>
            p.TickerId == tickerId && p.Timeframe == timeframeCode && p.CandleDate == candleDate);

        var open = sourceCandles.First().Open ?? sourceCandles.First().Close;
        var close = sourceCandles.Last().AdjustedClose ?? sourceCandles.Last().Close;
        var high = sourceCandles.Max(c => c.High ?? c.Close);
        var low = sourceCandles.Min(c => c.Low ?? c.Close);
        var volume = sourceCandles.Sum(c => c.Volume ?? 0);

        if (existing is null)
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
        else
        {
            existing.Open = open;
            existing.High = high;
            existing.Low = low;
            existing.Close = close;
            existing.AdjustedClose = close;
            existing.Volume = volume;
            existing.Source = "aggregated";
            existing.UpdatedUtc = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var offset = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        return date.AddDays(-offset);
    }
}
