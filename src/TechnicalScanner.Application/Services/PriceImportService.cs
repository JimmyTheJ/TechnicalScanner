using Microsoft.Extensions.Logging;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Domain.Entities;
using TechnicalScanner.Domain.Enums;

namespace TechnicalScanner.Application.Services;

public sealed class PriceImportService(
    IMarketDataProvider marketDataProvider,
    ITechnicalScannerDbContext dbContext,
    ILogger<PriceImportService> logger)
{
    public async Task<int> UpdatePricesAsync(int years, CancellationToken cancellationToken)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var startDate = endDate.AddYears(-years);
        var tickers = dbContext.Tickers.Where(t => t.IsActive).ToList();
        var updated = 0;

        foreach (var ticker in tickers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var candles = await marketDataProvider.GetHistoricalCandlesAsync(
                    ticker.Symbol,
                    ticker.Exchange,
                    ticker.Country,
                    Timeframe.D,
                    startDate,
                    endDate,
                    cancellationToken);

                foreach (var candle in candles)
                {
                    var existing = dbContext.PriceCandles.FirstOrDefault(p =>
                        p.TickerId == ticker.Id &&
                        p.Timeframe == Timeframe.D.ToCode() &&
                        p.CandleDate == candle.CandleDate);

                    if (existing is null)
                    {
                        dbContext.Add(new PriceCandle
                        {
                            TickerId = ticker.Id,
                            Timeframe = Timeframe.D.ToCode(),
                            CandleDate = candle.CandleDate,
                            Open = candle.Open,
                            High = candle.High,
                            Low = candle.Low,
                            Close = candle.Close,
                            AdjustedClose = candle.AdjustedClose,
                            Volume = candle.Volume,
                            Source = candle.Source
                        });
                    }
                    else
                    {
                        existing.Open = candle.Open;
                        existing.High = candle.High;
                        existing.Low = candle.Low;
                        existing.Close = candle.Close;
                        existing.AdjustedClose = candle.AdjustedClose;
                        existing.Volume = candle.Volume;
                        existing.Source = candle.Source;
                        existing.UpdatedUtc = DateTimeOffset.UtcNow;
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                updated++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to import daily candles for {Symbol}.", ticker.Symbol);
            }
        }

        logger.LogInformation("Updated daily prices for {Count}/{Total} tickers.", updated, tickers.Count);
        return updated;
    }
}
