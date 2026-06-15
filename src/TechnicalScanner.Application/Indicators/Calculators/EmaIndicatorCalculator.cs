using System.Text.Json;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Indicators;
using TechnicalScanner.Application.Models;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Indicators.Calculators;

public sealed class EmaIndicatorCalculator : IIndicatorCalculator
{
    public string IndicatorType => "EMA";

    public Task<IReadOnlyList<IndicatorValueResult>> CalculateAsync(
        Ticker ticker,
        IndicatorDefinition definition,
        IReadOnlyList<PriceCandle> candles,
        CancellationToken cancellationToken)
    {
        var parameters = JsonSerializer.Deserialize<MovingAverageParameters>(definition.ParametersJson, ApplicationJsonOptions.Default)
            ?? throw new InvalidOperationException($"Invalid EMA parameters for {definition.Code}.");

        var ordered = candles.OrderBy(c => c.CandleDate).ToList();
        var closes = ordered.Select(c => c.AdjustedClose ?? c.Close).ToList();
        var results = new List<IndicatorValueResult>();

        for (var i = parameters.Period - 1; i < closes.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var slice = closes.Take(i + 1).ToList();
            var ema = IndicatorMath.ExponentialMovingAverage(slice, parameters.Period);
            if (ema is null)
            {
                continue;
            }

            var close = closes[i];
            var distance = IndicatorMath.DistancePercent(close, ema.Value);
            var previousEma = i >= parameters.Period
                ? IndicatorMath.ExponentialMovingAverage(closes.Take(i).ToList(), parameters.Period)
                : null;

            var values = new Dictionary<string, object?>
            {
                ["close"] = close,
                ["ema"] = ema.Value,
                ["period"] = parameters.Period,
                ["distanceFromEmaPct"] = distance,
                ["closeAboveEma"] = close > ema.Value,
                ["closeBelowEma"] = close < ema.Value,
                ["emaSlopePositive"] = previousEma is not null && ema.Value > previousEma,
                ["emaSlopeNegative"] = previousEma is not null && ema.Value < previousEma
            };

            results.Add(new IndicatorValueResult(
                ordered[i].CandleDate,
                definition.Timeframe,
                distance,
                ema.Value,
                null,
                values));
        }

        return Task.FromResult<IReadOnlyList<IndicatorValueResult>>(results);
    }

    private sealed record MovingAverageParameters(int Period);
}
