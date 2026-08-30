using System.Text.Json;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Indicators;
using TechnicalScanner.Application.Models;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Indicators.Calculators;

public sealed class RocIndicatorCalculator : IIndicatorCalculator
{
    public string IndicatorType => "ROC";

    public Task<IReadOnlyList<IndicatorValueResult>> CalculateAsync(
        Ticker ticker,
        IndicatorDefinition definition,
        IReadOnlyList<PriceCandle> candles,
        CancellationToken cancellationToken)
    {
        var parameters = JsonSerializer.Deserialize<RocParameters>(definition.ParametersJson, ApplicationJsonOptions.Default)
            ?? throw new InvalidOperationException($"Invalid ROC parameters for {definition.Code}.");
        if (parameters.Period <= 0)
        {
            throw new InvalidOperationException($"ROC period must be positive for {definition.Code}.");
        }

        var ordered = candles.OrderBy(c => c.CandleDate).ToList();
        var results = new List<IndicatorValueResult>();

        for (var i = parameters.Period; i < ordered.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var close = ordered[i].AdjustedClose ?? ordered[i].Close;
            var priorClose = ordered[i - parameters.Period].AdjustedClose ?? ordered[i - parameters.Period].Close;
            var rocPct = IndicatorMath.DistancePercent(close, priorClose);
            if (rocPct is null)
            {
                continue;
            }

            var values = new Dictionary<string, object?>
            {
                ["close"] = close,
                ["priorClose"] = priorClose,
                ["rocPct"] = rocPct,
                ["period"] = parameters.Period
            };

            results.Add(new IndicatorValueResult(
                ordered[i].CandleDate,
                definition.Timeframe,
                rocPct,
                priorClose,
                null,
                values));
        }

        return Task.FromResult<IReadOnlyList<IndicatorValueResult>>(results);
    }

    private sealed record RocParameters(int Period);
}
