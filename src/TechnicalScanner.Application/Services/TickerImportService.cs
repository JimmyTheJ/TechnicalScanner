using System.Text.Json;
using Microsoft.Extensions.Logging;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Domain.Entities;
using TechnicalScanner.Domain.Enums;

namespace TechnicalScanner.Application.Services;

public sealed class TickerImportService(
    IMarketDataProvider marketDataProvider,
    ITechnicalScannerDbContext dbContext,
    ILogger<TickerImportService> logger)
{
    public async Task<int> ImportAsync(CancellationToken cancellationToken)
    {
        var tickers = await marketDataProvider.GetTickersAsync(cancellationToken);
        var imported = 0;

        foreach (var dto in tickers)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Imported or updated {Count} tickers ({Imported} new).", tickers.Count, imported);
        return imported;
    }
}
