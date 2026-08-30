using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Confluence;
using TechnicalScanner.Application.Models.Queries;
using TechnicalScanner.Application.Signals;

namespace TechnicalScanner.Application.Services.Queries;

public sealed class ConfluenceQueryService(
    ITechnicalScannerDbContext dbContext,
    SignalRuleEvaluator ruleEvaluator)
{
    public ConfluenceSnapshotDto? GetForSymbol(string symbol)
    {
        var ticker = dbContext.Tickers.FirstOrDefault(t => t.IsActive && t.Symbol == symbol);
        if (ticker is null)
        {
            return null;
        }

        var pair = LatestIndicatorLookup.Load(dbContext, [ticker.Id], ConfluenceCatalog.IndicatorCodes);
        var map = LatestIndicatorLookup.ToEvaluatorMap(pair.GetValueOrDefault(ticker.Id));
        var tally = ConfluenceScoring.Evaluate(map, ruleEvaluator);
        return ToDto(ticker.Symbol, tally);
    }

    public static ConfluenceSnapshotDto ToDto(string symbol, ConfluenceTally tally)
        => new()
        {
            Symbol = symbol,
            CheapHits = tally.CheapHits,
            CheapValid = tally.CheapValid,
            CheapPct = tally.CheapPct,
            ExtendedHits = tally.ExtendedHits,
            ExtendedValid = tally.ExtendedValid,
            ExtendedPct = tally.ExtendedPct,
            Bias = tally.Bias,
            Components = tally.Components.Select(component => new ConfluenceComponentDto
            {
                Label = component.Label,
                Timeframe = component.Timeframe,
                IndicatorCode = component.IndicatorCode,
                Field = component.Field,
                Operator = component.Operator,
                Side = component.Side,
                Available = component.Available,
                Matched = component.Matched
            }).ToList()
        };
}
