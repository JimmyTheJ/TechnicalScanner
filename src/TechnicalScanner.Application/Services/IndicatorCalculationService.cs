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
    public Task<int> CalculateAsync(CancellationToken cancellationToken)
        => CalculateAsync(null, pendingOnly: false, cancellationToken);

    public Task<int> CalculateAsync(string? indicatorCode, CancellationToken cancellationToken)
        => CalculateAsync(indicatorCode, pendingOnly: false, cancellationToken);

    public async Task<int> CalculateAsync(string? indicatorCode, bool pendingOnly, CancellationToken cancellationToken)
    {
        var definitions = dbContext.IndicatorDefinitions
            .Where(d => d.IsActive)
            .ToList()
            .Where(d => string.IsNullOrWhiteSpace(indicatorCode) ||
                        d.Code.Equals(indicatorCode, StringComparison.OrdinalIgnoreCase))
            .Where(d => !pendingOnly || d.NeedsRecalculation)
            .ToList();

        if (definitions.Count == 0)
        {
            logger.LogWarning("No active indicator definitions matched {IndicatorCode}.", indicatorCode);
            return 0;
        }

        var tickers = dbContext.Tickers.Where(t => t.IsActive).ToList();
        var calculated = 0;

        foreach (var ticker in tickers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candlesByTimeframe = dbContext.PriceCandles
                .Where(p => p.TickerId == ticker.Id)
                .ToList()
                .GroupBy(p => p.Timeframe)
                .ToDictionary(g => g.Key, g => g.OrderBy(c => c.CandleDate).ToList());

            foreach (var definition in definitions)
            {
                if (!candlesByTimeframe.TryGetValue(definition.Timeframe, out var candles) || candles.Count == 0)
                {
                    continue;
                }

                try
                {
                    var calculator = calculatorRegistry.GetRequired(definition.IndicatorType);
                    var results = await calculator.CalculateAsync(ticker, definition, candles, cancellationToken);
                    UpsertResults(ticker.Id, definition, results);
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

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        foreach (var definition in definitions)
        {
            definition.NeedsRecalculation = false;
            definition.UpdatedUtc = DateTimeOffset.UtcNow;
        }

        if (definitions.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Calculated {Count} ticker/indicator pairs{Filter}.",
            calculated,
            string.IsNullOrWhiteSpace(indicatorCode) ? string.Empty : $" for {indicatorCode}");
        return calculated;
    }

    private void UpsertResults(
        long tickerId,
        IndicatorDefinition definition,
        IReadOnlyList<Models.IndicatorValueResult> results)
    {
        var existing = dbContext.IndicatorValues
            .Where(v => v.TickerId == tickerId && v.IndicatorDefinitionId == definition.Id)
            .ToList()
            .ToDictionary(v => v.CandleDate);

        foreach (var result in results)
        {
            var valuesJson = JsonSerializer.Serialize(result.Values);
            if (existing.TryGetValue(result.CandleDate, out var row))
            {
                row.PrimaryValue = result.PrimaryValue;
                row.SecondaryValue = result.SecondaryValue;
                row.TertiaryValue = result.TertiaryValue;
                row.ValuesJson = valuesJson;
                row.UpdatedUtc = DateTimeOffset.UtcNow;
            }
            else
            {
                dbContext.Add(new IndicatorValue
                {
                    TickerId = tickerId,
                    IndicatorDefinitionId = definition.Id,
                    CandleDate = result.CandleDate,
                    Timeframe = result.Timeframe,
                    PrimaryValue = result.PrimaryValue,
                    SecondaryValue = result.SecondaryValue,
                    TertiaryValue = result.TertiaryValue,
                    ValuesJson = valuesJson
                });
            }
        }
    }
}
