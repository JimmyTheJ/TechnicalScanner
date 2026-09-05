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
    private const int ProgressLogInterval = 500;
    private const int MaxConsecutiveRateLimitedTickers = 2;

    public Task<int> UpdatePricesAsync(int years, CancellationToken cancellationToken)
        => UpdatePricesAsync(years, incremental: false, cancellationToken);

    public async Task<int> UpdatePricesAsync(int years, bool incremental, CancellationToken cancellationToken)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var defaultStartDate = endDate.AddYears(-years);
        var tickers = dbContext.Tickers.Where(t => t.IsActive).ToList();
        var updated = 0;
        var alreadyCurrent = 0;
        var failed = 0;
        var processed = 0;
        var consecutiveRateLimitedTickers = 0;

        foreach (var ticker in tickers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed++;

            try
            {
                var latestCandleDate = GetLatestDailyCandleDate(ticker.Id);
                if (incremental && IncrementalPriceFreshness.HasCurrentSession(latestCandleDate, endDate))
                {
                    alreadyCurrent++;
                    updated++;
                    LogProgressIfNeeded(processed, tickers.Count, updated, alreadyCurrent, failed);
                    continue;
                }

                var startDate = ResolveStartDate(latestCandleDate, defaultStartDate, incremental);
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
                consecutiveRateLimitedTickers = 0;
            }
            catch (MarketDataRateLimitedException ex)
            {
                failed++;
                consecutiveRateLimitedTickers++;
                await operationLogger.LogFailureAsync(
                    ticker.Symbol,
                    "update-prices",
                    ex.Message,
                    ticker.Id,
                    cancellationToken);

                if (consecutiveRateLimitedTickers >= MaxConsecutiveRateLimitedTickers)
                {
                    logger.LogError(
                        "Stopping price import after {RateLimited} consecutive tickers were rate-limited. Updated {Updated}/{Total} (already current: {AlreadyCurrent}). Remaining tickers will be fetched on a later incremental run.",
                        consecutiveRateLimitedTickers,
                        updated,
                        tickers.Count,
                        alreadyCurrent);
                    break;
                }
            }
            catch (Exception ex)
            {
                failed++;
                consecutiveRateLimitedTickers = 0;
                await operationLogger.LogFailureAsync(
                    ticker.Symbol,
                    "update-prices",
                    ex.Message,
                    ticker.Id,
                    cancellationToken);
            }

            LogProgressIfNeeded(processed, tickers.Count, updated, alreadyCurrent, failed);
        }

        logger.LogInformation(
            "Updated daily prices for {Count}/{Total} tickers (incremental={Incremental}, alreadyCurrent={AlreadyCurrent}, failed={Failed}).",
            updated,
            tickers.Count,
            incremental,
            alreadyCurrent,
            failed);

        return updated;
    }

    private void LogProgressIfNeeded(int processed, int total, int updated, int alreadyCurrent, int failed)
    {
        if (processed % ProgressLogInterval != 0)
        {
            return;
        }

        logger.LogInformation(
            "Price import progress: {Processed}/{Total} tickers ({Updated} updated, {AlreadyCurrent} already current, {Failed} failed).",
            processed,
            total,
            updated,
            alreadyCurrent,
            failed);
    }

    private DateOnly? GetLatestDailyCandleDate(long tickerId)
        => dbContext.PriceCandles
            .Where(p => p.TickerId == tickerId && p.Timeframe == Timeframe.D.ToCode())
            .OrderByDescending(p => p.CandleDate)
            .Select(p => (DateOnly?)p.CandleDate)
            .FirstOrDefault();

    private static DateOnly ResolveStartDate(DateOnly? latestCandleDate, DateOnly defaultStartDate, bool incremental)
    {
        if (!incremental || latestCandleDate is null)
        {
            return defaultStartDate;
        }

        var incrementalStart = latestCandleDate.Value.AddDays(-LookbackDaysForCorrections);
        return incrementalStart > defaultStartDate ? incrementalStart : defaultStartDate;
    }
}
