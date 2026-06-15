using System.Text.Json;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Indicators;
using TechnicalScanner.Application.Models;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Indicators.Calculators;

public sealed class SmaIndicatorCalculator : IIndicatorCalculator
{
    public string IndicatorType => "SMA";

    public Task<IReadOnlyList<IndicatorValueResult>> CalculateAsync(
        Ticker ticker,
        IndicatorDefinition definition,
        IReadOnlyList<PriceCandle> candles,
        CancellationToken cancellationToken)
    {
        var parameters = JsonSerializer.Deserialize<MovingAverageParameters>(definition.ParametersJson, ApplicationJsonOptions.Default)
            ?? throw new InvalidOperationException($"Invalid SMA parameters for {definition.Code}.");

        var ordered = candles.OrderBy(c => c.CandleDate).ToList();
        var closes = ordered.Select(c => c.AdjustedClose ?? c.Close).ToList();
        var results = new List<IndicatorValueResult>();

        for (var i = parameters.Period - 1; i < closes.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var slice = closes.Take(i + 1).ToList();
            var sma = IndicatorMath.SimpleMovingAverage(slice, parameters.Period);
            if (sma is null)
            {
                continue;
            }

            var close = closes[i];
            var distance = IndicatorMath.DistancePercent(close, sma.Value);
            var previousSma = i >= parameters.Period
                ? IndicatorMath.SimpleMovingAverage(closes.Take(i).ToList(), parameters.Period)
                : null;

            var values = new Dictionary<string, object?>
            {
                ["close"] = close,
                ["sma"] = sma.Value,
                ["period"] = parameters.Period,
                ["distanceFromSmaPct"] = distance,
                ["closeAboveSma"] = close > sma.Value,
                ["closeBelowSma"] = close < sma.Value,
                ["smaSlopePositive"] = previousSma is not null && sma.Value > previousSma,
                ["smaSlopeNegative"] = previousSma is not null && sma.Value < previousSma
            };

            results.Add(new IndicatorValueResult(
                ordered[i].CandleDate,
                definition.Timeframe,
                distance,
                sma.Value,
                null,
                values));
        }

        return Task.FromResult<IReadOnlyList<IndicatorValueResult>>(results);
    }

    private sealed record MovingAverageParameters(int Period);
}
