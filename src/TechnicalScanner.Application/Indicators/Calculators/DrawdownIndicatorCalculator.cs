using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Indicators;
using TechnicalScanner.Application.Models;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Indicators.Calculators;

public sealed class DrawdownIndicatorCalculator : IIndicatorCalculator
{
    public string IndicatorType => "DRAWDOWN";

    public Task<IReadOnlyList<IndicatorValueResult>> CalculateAsync(
        Ticker ticker,
        IndicatorDefinition definition,
        IReadOnlyList<PriceCandle> candles,
        CancellationToken cancellationToken)
    {
        var ordered = candles.OrderBy(c => c.CandleDate).ToList();
        var results = new List<IndicatorValueResult>();
        decimal? ath = null;

        for (var i = 0; i < ordered.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candle = ordered[i];
            var close = candle.AdjustedClose ?? candle.Close;
            var high = candle.High ?? close;
            if (close > high)
            {
                high = close;
            }

            ath = ath is null ? high : Math.Max(ath.Value, high);

            var distanceFromAthPct = IndicatorMath.DistancePercent(close, ath.Value);
            var drawdownPct = ath.Value == 0 ? (decimal?)null : (ath.Value - close) / ath.Value * 100m;
            var values = new Dictionary<string, object?>
            {
                ["close"] = close,
                ["ath"] = ath,
                ["distanceFromAthPct"] = distanceFromAthPct,
                ["drawdownPct"] = drawdownPct
            };

            results.Add(new IndicatorValueResult(
                candle.CandleDate,
                definition.Timeframe,
                distanceFromAthPct,
                ath,
                drawdownPct,
                values));
        }

        return Task.FromResult<IReadOnlyList<IndicatorValueResult>>(results);
    }
}
