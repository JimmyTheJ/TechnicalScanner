using Microsoft.Extensions.Logging;
using System.Text.Json;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Indicators;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Services;

public sealed class IndicatorCalculationService(
    ITechnicalScannerDbContext dbContext,
    IndicatorCalculatorRegistry calculatorRegistry,
    ILogger<IndicatorCalculationService> logger)
{
    public async Task<int> CalculateAsync(CancellationToken cancellationToken)
    {
        var definitions = dbContext.IndicatorDefinitions.Where(d => d.IsActive).ToList();
        var tickers = dbContext.Tickers.Where(t => t.IsActive).ToList();
        var calculated = 0;

        foreach (var ticker in tickers)
        {
            foreach (var definition in definitions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var candles = dbContext.PriceCandles
                        .Where(p => p.TickerId == ticker.Id && p.Timeframe == definition.Timeframe)
                        .OrderBy(p => p.CandleDate)
                        .ToList();

                    if (candles.Count == 0)
                    {
                        continue;
                    }

                    var calculator = calculatorRegistry.GetRequired(definition.IndicatorType);
                    var results = await calculator.CalculateAsync(ticker, definition, candles, cancellationToken);

                    foreach (var result in results)
                    {
                        var existing = dbContext.IndicatorValues.FirstOrDefault(v =>
                            v.TickerId == ticker.Id &&
                            v.IndicatorDefinitionId == definition.Id &&
                            v.CandleDate == result.CandleDate);

                        var valuesJson = JsonSerializer.Serialize(result.Values);

                        if (existing is null)
                        {
                            dbContext.Add(new IndicatorValue
                            {
                                TickerId = ticker.Id,
                                IndicatorDefinitionId = definition.Id,
                                CandleDate = result.CandleDate,
                                Timeframe = result.Timeframe,
                                PrimaryValue = result.PrimaryValue,
                                SecondaryValue = result.SecondaryValue,
                                TertiaryValue = result.TertiaryValue,
                                ValuesJson = valuesJson
                            });
                        }
                        else
                        {
                            existing.PrimaryValue = result.PrimaryValue;
                            existing.SecondaryValue = result.SecondaryValue;
                            existing.TertiaryValue = result.TertiaryValue;
                            existing.ValuesJson = valuesJson;
                            existing.UpdatedUtc = DateTimeOffset.UtcNow;
                        }
                    }

                    await dbContext.SaveChangesAsync(cancellationToken);
                    calculated++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to calculate {Indicator} for {Symbol}.",
                        definition.Code,
                        ticker.Symbol);
                }
            }
        }

        logger.LogInformation("Calculated {Count} ticker/indicator pairs.", calculated);
        return calculated;
    }
}
