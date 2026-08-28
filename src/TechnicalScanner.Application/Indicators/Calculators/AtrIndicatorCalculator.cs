using System.Text.Json;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Indicators;
using TechnicalScanner.Application.Models;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Indicators.Calculators;

public sealed class AtrIndicatorCalculator : IIndicatorCalculator
{
    public string IndicatorType => "ATR";

    public Task<IReadOnlyList<IndicatorValueResult>> CalculateAsync(
        Ticker ticker,
        IndicatorDefinition definition,
        IReadOnlyList<PriceCandle> candles,
        CancellationToken cancellationToken)
    {
        var parameters = JsonSerializer.Deserialize<AtrParameters>(definition.ParametersJson, ApplicationJsonOptions.Default)
            ?? throw new InvalidOperationException($"Invalid ATR parameters for {definition.Code}.");

        var ordered = candles.OrderBy(c => c.CandleDate).ToList();
        var results = new List<IndicatorValueResult>();
        decimal? atr = null;
        decimal trSum = 0;

        for (var i = 0; i < ordered.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candle = ordered[i];
            var close = candle.AdjustedClose ?? candle.Close;
            var high = candle.High ?? close;
            var low = candle.Low ?? close;
            var previousClose = i == 0 ? (decimal?)null : ordered[i - 1].AdjustedClose ?? ordered[i - 1].Close;
            var trueRange = IndicatorMath.TrueRange(high, low, previousClose);

            if (i < parameters.Period)
            {
                trSum += trueRange;
                if (i == parameters.Period - 1)
                {
                    atr = trSum / parameters.Period;
                }
                else
                {
                    continue;
                }
            }
            else
            {
                atr = ((atr!.Value * (parameters.Period - 1)) + trueRange) / parameters.Period;
            }

            var atrPct = close == 0 ? (decimal?)null : atr.Value / close * 100m;
            var values = new Dictionary<string, object?>
            {
                ["close"] = close,
                ["atr"] = atr.Value,
                ["atrPct"] = atrPct,
                ["trueRange"] = trueRange,
                ["period"] = parameters.Period
            };

            results.Add(new IndicatorValueResult(
                candle.CandleDate,
                definition.Timeframe,
                atrPct,
                atr.Value,
                trueRange,
                values));
        }

        return Task.FromResult<IReadOnlyList<IndicatorValueResult>>(results);
    }

    private sealed record AtrParameters(int Period);
}
