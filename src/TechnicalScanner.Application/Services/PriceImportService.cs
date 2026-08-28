using Microsoft.Extensions.Logging;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Domain.Entities;
using TechnicalScanner.Domain.Enums;

namespace TechnicalScanner.Application.Services;

public sealed class PriceImportService(
    IMarketDataProvider marketDataProvider,
    ITechnicalScannerDbContext dbContext,
    ITickerOperationLogger operationLogger,
    ILogger<PriceImportService> logger)
{
    private const int LookbackDaysForCorrections = 5;

    public Task<int> UpdatePricesAsync(int years, CancellationToken cancellationToken)
        => UpdatePricesAsync(years, incremental: false, cancellationToken);

    public async Task<int> UpdatePricesAsync(int years, bool incremental, CancellationToken cancellationToken)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var defaultStartDate = endDate.AddYears(-years);
        var tickers = dbContext.Tickers.Where(t => t.IsActive).ToList();
        var updated = 0;

        foreach (var ticker in tickers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var startDate = ResolveStartDate(ticker.Id, defaultStartDate, incremental);
                if (startDate > endDate)
                {
                    continue;
                }

                var candles = await marketDataProvider.GetHistoricalCandlesAsync(
                    ticker.Symbol,
                    ticker.Exchange,
                    ticker.Country,
                    Timeframe.D,
                    startDate,
                    endDate,
                    cancellationToken);

                var timeframeCode = Timeframe.D.ToCode();
                var existing = dbContext.PriceCandles
                    .Where(p =>
                        p.TickerId == ticker.Id &&
                        p.Timeframe == timeframeCode &&
                        p.CandleDate >= startDate &&
                        p.CandleDate <= endDate)
                    .ToList()
                    .ToDictionary(p => p.CandleDate);

                foreach (var candle in candles)
                {
                    if (existing.TryGetValue(candle.CandleDate, out var row))
                    {
                        row.Open = candle.Open;
                        row.High = candle.High;
                        row.Low = candle.Low;
                        row.Close = candle.Close;
                        row.AdjustedClose = candle.AdjustedClose;
                        row.Volume = candle.Volume;
                        row.Source = candle.Source;
                        row.UpdatedUtc = DateTimeOffset.UtcNow;
                    }
                    else
                    {
                        var added = new PriceCandle
                        {
                            TickerId = ticker.Id,
                            Timeframe = timeframeCode,
                            CandleDate = candle.CandleDate,
                            Open = candle.Open,
                            High = candle.High,
                            Low = candle.Low,
                            Close = candle.Close,
                            AdjustedClose = candle.AdjustedClose,
                            Volume = candle.Volume,
                            Source = candle.Source
                        };
                        dbContext.Add(added);
                        existing[candle.CandleDate] = added;
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                updated++;
            }
            catch (Exception ex)
            {
                await operationLogger.LogFailureAsync(
                    ticker.Symbol,
                    "update-prices",
                    ex.Message,
                    ticker.Id,
                    cancellationToken);
            }
        }

        logger.LogInformation(
            "Updated daily prices for {Count}/{Total} tickers (incremental={Incremental}).",
            updated,
            tickers.Count,
            incremental);

        return updated;
    }

    private DateOnly ResolveStartDate(long tickerId, DateOnly defaultStartDate, bool incremental)
    {
        if (!incremental)
        {
            return defaultStartDate;
        }

        var latestCandleDate = dbContext.PriceCandles
            .Where(p => p.TickerId == tickerId && p.Timeframe == Timeframe.D.ToCode())
            .OrderByDescending(p => p.CandleDate)
            .Select(p => (DateOnly?)p.CandleDate)
            .FirstOrDefault();

        if (latestCandleDate is null)
        {
            return defaultStartDate;
        }

        var incrementalStart = latestCandleDate.Value.AddDays(-LookbackDaysForCorrections);
        return incrementalStart > defaultStartDate ? incrementalStart : defaultStartDate;
    }
}
