using Microsoft.Extensions.Logging;
using System.Text.Json;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Signals;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Services;

public sealed class SignalEvaluationService(
    ITechnicalScannerDbContext dbContext,
    SignalRuleEvaluator ruleEvaluator,
    ILogger<SignalEvaluationService> logger)
{
    public async Task<int> EvaluateAsync(CancellationToken cancellationToken)
    {
        var scanDate = DateTimeOffset.UtcNow;
        var signals = dbContext.SignalDefinitions.Where(s => s.IsActive).ToList();
        var tickers = dbContext.Tickers.Where(t => t.IsActive).ToList();
        var evaluated = 0;

        foreach (var ticker in tickers)
        {
            var latestValues = dbContext.IndicatorValues
                .Where(v => v.TickerId == ticker.Id)
                .GroupBy(v => v.IndicatorDefinitionId)
                .Select(g => g.OrderByDescending(v => v.CandleDate).First())
                .ToList();

            var valuesByCode = latestValues
                .Join(
                    dbContext.IndicatorDefinitions,
                    value => value.IndicatorDefinitionId,
                    definition => definition.Id,
                    (value, definition) => new { definition.Code, value })
                .ToDictionary(x => x.Code, x => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(x.value.ValuesJson)
                    ?? new Dictionary<string, JsonElement>());

            foreach (var signal in signals)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var isMatch = ruleEvaluator.Evaluate(signal.RuleJson, valuesByCode);
                var existing = dbContext.SignalResults.FirstOrDefault(r =>
                    r.ScanDateUtc == scanDate &&
                    r.TickerId == ticker.Id &&
                    r.SignalDefinitionId == signal.Id);

                if (existing is null)
                {
                    dbContext.Add(new SignalResult
                    {
                        ScanDateUtc = scanDate,
                        TickerId = ticker.Id,
                        SignalDefinitionId = signal.Id,
                        IsMatch = isMatch,
                        ValuesJson = JsonSerializer.Serialize(valuesByCode)
                    });
                }
                else
                {
                    existing.IsMatch = isMatch;
                    existing.ValuesJson = JsonSerializer.Serialize(valuesByCode);
                }

                evaluated++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Evaluated {Count} signal results.", evaluated);
        return evaluated;
    }
}
