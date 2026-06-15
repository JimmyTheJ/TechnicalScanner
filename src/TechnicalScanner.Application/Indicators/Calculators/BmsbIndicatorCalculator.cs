using System.Text.Json;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Indicators;
using TechnicalScanner.Application.Models;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Indicators.Calculators;

public sealed class BmsbIndicatorCalculator : IIndicatorCalculator
{
    public string IndicatorType => "BMSB";

    public Task<IReadOnlyList<IndicatorValueResult>> CalculateAsync(
        Ticker ticker,
        IndicatorDefinition definition,
        IReadOnlyList<PriceCandle> candles,
        CancellationToken cancellationToken)
    {
        var parameters = JsonSerializer.Deserialize<BmsbParameters>(definition.ParametersJson, ApplicationJsonOptions.Default)
            ?? throw new InvalidOperationException($"Invalid BMSB parameters for {definition.Code}.");

        var closes = candles
            .OrderBy(c => c.CandleDate)
            .Select(c => c.AdjustedClose ?? c.Close)
            .ToList();

        var results = new List<IndicatorValueResult>();
        var minPeriod = Math.Max(parameters.SmaPeriod, parameters.EmaPeriod);

        for (var i = minPeriod - 1; i < closes.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var slice = closes.Take(i + 1).ToList();
            var sma = IndicatorMath.SimpleMovingAverage(slice, parameters.SmaPeriod);
            var ema = IndicatorMath.ExponentialMovingAverage(slice, parameters.EmaPeriod);

            if (sma is null || ema is null)
            {
                continue;
            }

            var close = closes[i];
            var bandTop = Math.Max(sma.Value, ema.Value);
            var bandBottom = Math.Min(sma.Value, ema.Value);
            var bandMidpoint = (sma.Value + ema.Value) / 2m;
            var status = close < bandBottom ? "BelowBand"
                : close > bandTop ? "AboveBand"
                : "InsideBand";

            var values = new Dictionary<string, object?>
            {
                ["close"] = close,
                ["sma20w"] = sma.Value,
                ["ema21w"] = ema.Value,
                ["bandTop"] = bandTop,
                ["bandBottom"] = bandBottom,
                ["bandMidpoint"] = bandMidpoint,
                ["distanceFromBandBottomPct"] = IndicatorMath.DistancePercent(close, bandBottom),
                ["distanceFromBandTopPct"] = IndicatorMath.DistancePercent(close, bandTop),
                ["distanceFromBandMidpointPct"] = IndicatorMath.DistancePercent(close, bandMidpoint),
                ["status"] = status
            };

            results.Add(new IndicatorValueResult(
                candles[i].CandleDate,
                definition.Timeframe,
                IndicatorMath.DistancePercent(close, bandMidpoint),
                bandTop,
                bandBottom,
                values));
        }

        return Task.FromResult<IReadOnlyList<IndicatorValueResult>>(results);
    }

    private sealed record BmsbParameters(int SmaPeriod, int EmaPeriod);
}
