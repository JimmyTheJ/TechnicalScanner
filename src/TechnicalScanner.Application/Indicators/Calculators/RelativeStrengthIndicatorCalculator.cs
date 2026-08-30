using System.Text.Json;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Indicators;
using TechnicalScanner.Application.Models;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Indicators.Calculators;

public sealed class RelativeStrengthIndicatorCalculator(IBenchmarkPriceLookup benchmarkPrices) : IIndicatorCalculator
{
    public string IndicatorType => "RELATIVE_STRENGTH";

    public Task<IReadOnlyList<IndicatorValueResult>> CalculateAsync(
        Ticker ticker,
        IndicatorDefinition definition,
        IReadOnlyList<PriceCandle> candles,
        CancellationToken cancellationToken)
    {
        var parameters = JsonSerializer.Deserialize<RelativeStrengthParameters>(
                definition.ParametersJson,
                ApplicationJsonOptions.Default)
            ?? throw new InvalidOperationException($"Invalid RELATIVE_STRENGTH parameters for {definition.Code}.");
        if (parameters.Period <= 0)
        {
            throw new InvalidOperationException($"RELATIVE_STRENGTH period must be positive for {definition.Code}.");
        }

        var benchmarkSymbol = string.IsNullOrWhiteSpace(parameters.BenchmarkSymbol)
            ? "SPY"
            : parameters.BenchmarkSymbol.Trim().ToUpperInvariant();
        var benchmark = benchmarkPrices.GetCloses(benchmarkSymbol, definition.Timeframe);
        if (benchmark.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<IndicatorValueResult>>([]);
        }

        var ordered = candles.OrderBy(c => c.CandleDate).ToList();
        var results = new List<IndicatorValueResult>();

        for (var i = parameters.Period; i < ordered.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candle = ordered[i];
            var prior = ordered[i - parameters.Period];
            var close = candle.AdjustedClose ?? candle.Close;
            var priorClose = prior.AdjustedClose ?? prior.Close;
            var stockReturnPct = IndicatorMath.DistancePercent(close, priorClose);
            var benchClose = IndicatorMath.CloseOnOrBefore(benchmark, candle.CandleDate);
            var benchPrior = IndicatorMath.CloseOnOrBefore(benchmark, prior.CandleDate);
            if (stockReturnPct is null || benchClose is null || benchPrior is null)
            {
                continue;
            }

            var benchmarkReturnPct = IndicatorMath.DistancePercent(benchClose.Value, benchPrior.Value);
            if (benchmarkReturnPct is null)
            {
                continue;
            }

            var relativeStrengthPct = stockReturnPct.Value - benchmarkReturnPct.Value;
            var values = new Dictionary<string, object?>
            {
                ["close"] = close,
                ["priorClose"] = priorClose,
                ["stockReturnPct"] = stockReturnPct,
                ["benchmarkReturnPct"] = benchmarkReturnPct,
                ["relativeStrengthPct"] = relativeStrengthPct,
                ["benchmarkSymbol"] = benchmarkSymbol,
                ["period"] = parameters.Period
            };

            results.Add(new IndicatorValueResult(
                candle.CandleDate,
                definition.Timeframe,
                relativeStrengthPct,
                stockReturnPct,
                benchmarkReturnPct,
                values));
        }

        return Task.FromResult<IReadOnlyList<IndicatorValueResult>>(results);
    }

    private sealed record RelativeStrengthParameters(int Period, string? BenchmarkSymbol);
}
