using System.Text.Json;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Indicators;
using TechnicalScanner.Application.Models;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Indicators.Calculators;

public sealed class VolumeIndicatorCalculator : IIndicatorCalculator
{
    public string IndicatorType => "VOLUME";

    public Task<IReadOnlyList<IndicatorValueResult>> CalculateAsync(
        Ticker ticker,
        IndicatorDefinition definition,
        IReadOnlyList<PriceCandle> candles,
        CancellationToken cancellationToken)
    {
        var parameters = JsonSerializer.Deserialize<VolumeParameters>(definition.ParametersJson, ApplicationJsonOptions.Default)
            ?? throw new InvalidOperationException($"Invalid VOLUME parameters for {definition.Code}.");

        var ordered = candles.OrderBy(c => c.CandleDate).ToList();
        var volumes = ordered.Select(c => (decimal)(c.Volume ?? 0)).ToList();
        var closes = ordered.Select(c => c.AdjustedClose ?? c.Close).ToList();
        var results = new List<IndicatorValueResult>();

        for (var i = parameters.Period - 1; i < volumes.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var slice = volumes.Take(i + 1).ToList();
            var volumeSma = IndicatorMath.SimpleMovingAverage(slice, parameters.Period);
            if (volumeSma is null)
            {
                continue;
            }

            var volume = volumes[i];
            var close = closes[i];
            var relativeVolume = volumeSma.Value == 0 ? (decimal?)null : volume / volumeSma.Value;
            var dollarVolume = close * volume;

            var values = new Dictionary<string, object?>
            {
                ["close"] = close,
                ["volume"] = volume,
                ["volumeSma"] = volumeSma.Value,
                ["relativeVolume"] = relativeVolume,
                ["dollarVolume"] = dollarVolume,
                ["period"] = parameters.Period
            };

            results.Add(new IndicatorValueResult(
                ordered[i].CandleDate,
                definition.Timeframe,
                relativeVolume,
                volumeSma.Value,
                dollarVolume,
                values));
        }

        return Task.FromResult<IReadOnlyList<IndicatorValueResult>>(results);
    }

    private sealed record VolumeParameters(int Period);
}
