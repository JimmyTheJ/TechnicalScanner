using Microsoft.Extensions.Logging;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Services;

public sealed class TickerImportService(
    IMarketDataProvider marketDataProvider,
    ITechnicalScannerDbContext dbContext,
    ITickerOperationLogger operationLogger,
    ILogger<TickerImportService> logger)
{
    public async Task<int> ImportAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Application.Models.TickerDto> tickers;
        try
        {
            tickers = await marketDataProvider.GetTickersAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await operationLogger.LogFailureAsync("UNIVERSE", "import-tickers", ex.Message, cancellationToken: cancellationToken);
            throw;
        }

        var imported = 0;
        var activeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dto in tickers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var key = $"{dto.Symbol}|{dto.Exchange}|{dto.Country}";
            activeKeys.Add(key);

            var existing = dbContext.Tickers
                .FirstOrDefault(t => t.Symbol == dto.Symbol && t.Exchange == dto.Exchange && t.Country == dto.Country);

            if (existing is null)
            {
                dbContext.Add(new Ticker
                {
                    Symbol = dto.Symbol,
                    Exchange = dto.Exchange,
                    Country = dto.Country,
                    Currency = dto.Currency,
                    CompanyName = dto.CompanyName,
                    Sector = dto.Sector,
                    Industry = dto.Industry,
                    MarketCap = dto.MarketCap,
                    IsEtf = dto.IsEtf
                });
                imported++;
            }
            else
            {
                existing.Currency = dto.Currency;
                existing.CompanyName = dto.CompanyName;
                existing.Sector = dto.Sector;
                existing.Industry = dto.Industry;
                existing.MarketCap = dto.MarketCap;
                existing.IsEtf = dto.IsEtf;
                existing.IsActive = true;
                existing.UpdatedUtc = DateTimeOffset.UtcNow;
            }
        }

        var deactivated = 0;
        foreach (var ticker in dbContext.Tickers.Where(t => t.IsActive).ToList())
        {
            var key = $"{ticker.Symbol}|{ticker.Exchange}|{ticker.Country}";
            if (!activeKeys.Contains(key))
            {
                ticker.IsActive = false;
                ticker.UpdatedUtc = DateTimeOffset.UtcNow;
                deactivated++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Imported or updated {Count} tickers ({Imported} new, {Deactivated} deactivated).",
            tickers.Count,
            imported,
            deactivated);

        return imported;
    }
}
