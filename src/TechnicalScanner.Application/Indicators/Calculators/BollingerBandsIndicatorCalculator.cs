using System.Text.Json;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Indicators;
using TechnicalScanner.Application.Models;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Indicators.Calculators;

public sealed class BollingerBandsIndicatorCalculator : IIndicatorCalculator
{
    public string IndicatorType => "BOLLINGER_BANDS";

    public Task<IReadOnlyList<IndicatorValueResult>> CalculateAsync(
        Ticker ticker,
        IndicatorDefinition definition,
        IReadOnlyList<PriceCandle> candles,
        CancellationToken cancellationToken)
    {
        var parameters = JsonSerializer.Deserialize<BollingerParameters>(definition.ParametersJson, ApplicationJsonOptions.Default)
            ?? throw new InvalidOperationException($"Invalid Bollinger parameters for {definition.Code}.");

        var ordered = candles.OrderBy(c => c.CandleDate).ToList();
        var closes = ordered.Select(c => c.AdjustedClose ?? c.Close).ToList();
        var results = new List<IndicatorValueResult>();

        for (var i = parameters.Period - 1; i < closes.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var slice = closes.Take(i + 1).ToList();
            var (middle, upper, lower, _) = IndicatorMath.BollingerBands(
                slice,
                parameters.Period,
                parameters.StdDevMultiplier);

            if (middle is null || upper is null || lower is null)
            {
                continue;
            }

            var close = closes[i];
            var bandWidthPct = middle.Value == 0 ? (decimal?)null : ((upper.Value - lower.Value) / middle.Value) * 100m;
            var percentB = upper.Value == lower.Value
                ? 0.5m
                : (close - lower.Value) / (upper.Value - lower.Value);

            var status = close < lower.Value ? "CloseBelowLowerBand"
                : close > upper.Value ? "CloseAboveUpperBand"
                : percentB <= 0.2m ? "CloseNearLowerBand"
                : percentB >= 0.8m ? "CloseNearUpperBand"
                : "InsideBands";

            var values = new Dictionary<string, object?>
            {
                ["close"] = close,
                ["middleBand"] = middle.Value,
                ["upperBand"] = upper.Value,
                ["lowerBand"] = lower.Value,
                ["bandWidthPct"] = bandWidthPct,
                ["percentB"] = percentB,
                ["status"] = status
            };

            results.Add(new IndicatorValueResult(
                ordered[i].CandleDate,
                definition.Timeframe,
                percentB,
                bandWidthPct,
                null,
                values));
        }

        return Task.FromResult<IReadOnlyList<IndicatorValueResult>>(results);
    }

    private sealed record BollingerParameters(int Period, decimal StdDevMultiplier);
}
