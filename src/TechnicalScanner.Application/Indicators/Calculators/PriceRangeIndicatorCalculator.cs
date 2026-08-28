using System.Text.Json;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Indicators;
using TechnicalScanner.Application.Models;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Indicators.Calculators;

public sealed class PriceRangeIndicatorCalculator : IIndicatorCalculator
{
    public string IndicatorType => "PRICE_RANGE";

    public Task<IReadOnlyList<IndicatorValueResult>> CalculateAsync(
        Ticker ticker,
        IndicatorDefinition definition,
        IReadOnlyList<PriceCandle> candles,
        CancellationToken cancellationToken)
    {
        var parameters = JsonSerializer.Deserialize<PriceRangeParameters>(definition.ParametersJson, ApplicationJsonOptions.Default)
            ?? throw new InvalidOperationException($"Invalid PRICE_RANGE parameters for {definition.Code}.");

        var ordered = candles.OrderBy(c => c.CandleDate).ToList();
        var results = new List<IndicatorValueResult>();

        for (var i = parameters.Period - 1; i < ordered.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var windowStart = i - parameters.Period + 1;
            decimal periodHigh = decimal.MinValue;
            decimal periodLow = decimal.MaxValue;
            for (var j = windowStart; j <= i; j++)
            {
                var high = ordered[j].High ?? ordered[j].AdjustedClose ?? ordered[j].Close;
                var low = ordered[j].Low ?? ordered[j].AdjustedClose ?? ordered[j].Close;
                if (high > periodHigh)
                {
                    periodHigh = high;
                }

                if (low < periodLow)
                {
                    periodLow = low;
                }
            }

            var close = ordered[i].AdjustedClose ?? ordered[i].Close;
            var distanceFromHighPct = IndicatorMath.DistancePercent(close, periodHigh);
            var distanceFromLowPct = IndicatorMath.DistancePercent(close, periodLow);
            var range = periodHigh - periodLow;
            var rangePosition = range == 0 ? 0.5m : (close - periodLow) / range;

            var values = new Dictionary<string, object?>
            {
                ["close"] = close,
                ["periodHigh"] = periodHigh,
                ["periodLow"] = periodLow,
                ["distanceFromHighPct"] = distanceFromHighPct,
                ["distanceFromLowPct"] = distanceFromLowPct,
                ["rangePosition"] = rangePosition,
                ["period"] = parameters.Period
            };

            results.Add(new IndicatorValueResult(
                ordered[i].CandleDate,
                definition.Timeframe,
                distanceFromHighPct,
                distanceFromLowPct,
                rangePosition,
                values));
        }

        return Task.FromResult<IReadOnlyList<IndicatorValueResult>>(results);
    }

    private sealed record PriceRangeParameters(int Period);
}
