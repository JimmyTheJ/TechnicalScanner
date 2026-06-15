using System.Text.Json;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Indicators;
using TechnicalScanner.Application.Models;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Indicators.Calculators;

public sealed class RsiIndicatorCalculator : IIndicatorCalculator
{
    public string IndicatorType => "RSI";

    public Task<IReadOnlyList<IndicatorValueResult>> CalculateAsync(
        Ticker ticker,
        IndicatorDefinition definition,
        IReadOnlyList<PriceCandle> candles,
        CancellationToken cancellationToken)
    {
        var parameters = JsonSerializer.Deserialize<RsiParameters>(definition.ParametersJson, ApplicationJsonOptions.Default)
            ?? throw new InvalidOperationException($"Invalid RSI parameters for {definition.Code}.");

        var ordered = candles.OrderBy(c => c.CandleDate).ToList();
        var closes = ordered.Select(c => c.AdjustedClose ?? c.Close).ToList();
        var results = new List<IndicatorValueResult>();

        for (var i = parameters.Period; i < closes.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var slice = closes.Take(i + 1).ToList();
            var rsi = IndicatorMath.RelativeStrengthIndex(slice, parameters.Period);
            if (rsi is null)
            {
                continue;
            }

            var close = closes[i];
            var category = IndicatorMath.ClassifyRsi(rsi.Value);
            var values = new Dictionary<string, object?>
            {
                ["close"] = close,
                ["rsi"] = rsi.Value,
                ["period"] = parameters.Period,
                ["category"] = category
            };

            results.Add(new IndicatorValueResult(
                ordered[i].CandleDate,
                definition.Timeframe,
                rsi.Value,
                null,
                null,
                values));
        }

        return Task.FromResult<IReadOnlyList<IndicatorValueResult>>(results);
    }

    private sealed record RsiParameters(int Period);
}
