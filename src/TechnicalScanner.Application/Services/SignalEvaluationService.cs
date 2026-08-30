using Microsoft.Extensions.Logging;
using System.Text.Json;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Services.Queries;
using TechnicalScanner.Application.Signals;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Services;

public sealed class SignalEvaluationService(
    ITechnicalScannerDbContext dbContext,
    SignalRuleEvaluator ruleEvaluator,
    ILogger<SignalEvaluationService> logger)
{
    public async Task<int> EvaluateAsync(long? scanRunId = null, CancellationToken cancellationToken = default)
    {
        var scanDate = DateTimeOffset.UtcNow;
        var signals = dbContext.SignalDefinitions.Where(s => s.IsActive).ToList();
        var tickers = dbContext.Tickers.Where(t => t.IsActive).ToList();
        if (tickers.Count == 0 || signals.Count == 0)
        {
            return 0;
        }

        var codes = dbContext.IndicatorDefinitions.Where(d => d.IsActive).Select(d => d.Code).ToList();
        var pair = LatestIndicatorLookup.LoadCurrentAndPrevious(
            dbContext,
            tickers.Select(t => t.Id).ToList(),
            codes);
        var evaluated = 0;

        foreach (var ticker in tickers)
        {
            var current = LatestIndicatorLookup.ToEvaluatorMap(pair.Current.GetValueOrDefault(ticker.Id));
            var previous = LatestIndicatorLookup.ToEvaluatorMap(pair.Previous.GetValueOrDefault(ticker.Id));
            var valuesJson = JsonSerializer.Serialize(current);

            foreach (var signal in signals)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var isMatch = ruleEvaluator.Evaluate(signal.RuleJson, current, previous);
                var existing = dbContext.SignalResults.FirstOrDefault(r =>
                    r.ScanDateUtc == scanDate &&
                    r.TickerId == ticker.Id &&
                    r.SignalDefinitionId == signal.Id);

                if (existing is null)
                {
                    dbContext.Add(new SignalResult
                    {
                        ScanRunId = scanRunId,
                        ScanDateUtc = scanDate,
                        TickerId = ticker.Id,
                        SignalDefinitionId = signal.Id,
                        IsMatch = isMatch,
                        ValuesJson = valuesJson
                    });
                }
                else
                {
                    existing.ScanRunId = scanRunId;
                    existing.IsMatch = isMatch;
                    existing.ValuesJson = valuesJson;
                }

                evaluated++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Evaluated {Count} signal results.", evaluated);
        return evaluated;
    }
}
