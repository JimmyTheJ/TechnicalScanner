using Microsoft.Extensions.Logging;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Services;

public sealed class BreadthCalculationService(
    ITechnicalScannerDbContext dbContext,
    ILogger<BreadthCalculationService> logger)
{
    private static readonly (string MetricCode, string IndicatorCode, string Field, string Operator, object Threshold)[] Metrics =
    [
        ("BMSB_W_ABOVE_PCT", "BMSB_W", "status", "=", "AboveBand"),
        ("BMSB_W_BELOW_PCT", "BMSB_W", "status", "=", "BelowBand"),
        ("RSI_14_D_OVERSOLD_PCT", "RSI_14_D", "category", "=", "Oversold"),
        ("RSI_14_W_ABOVE_50_PCT", "RSI_14_W", "rsi", ">=", 50m),
        ("SMA_200_D_ABOVE_PCT", "SMA_200_D", "closeAboveSma", "=", true),
        ("SMA_50_W_ABOVE_PCT", "SMA_50_W", "closeAboveSma", "=", true),
        ("BB_20_2_D_BELOW_LOWER_PCT", "BB_20_2_D", "status", "=", "CloseBelowLowerBand")
    ];

    public async Task<int> CalculateAsync(long? scanRunId = null, CancellationToken cancellationToken = default)
    {
        var scanDate = DateTimeOffset.UtcNow;
        var tickers = dbContext.Tickers.Where(t => t.IsActive).ToList();
        var latestValues = tickers
            .Select(t => new
            {
                Ticker = t,
                Values = GetLatestIndicatorValues(t.Id)
            })
            .ToList();

        var count = 0;
        foreach (var metric in Metrics)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var valid = latestValues.Where(x => x.Values.ContainsKey(metric.IndicatorCode)).ToList();
            var matches = valid.Count(x => Matches(x.Values[metric.IndicatorCode], metric.Field, metric.Operator, metric.Threshold));
            var matchPct = valid.Count == 0 ? 0 : (decimal)matches / valid.Count * 100m;

            dbContext.Add(new BreadthSnapshot
            {
                ScanRunId = scanRunId,
                ScanDateUtc = scanDate,
                UniverseName = "ALL",
                MetricCode = metric.MetricCode,
                TotalValidStocks = valid.Count,
                MatchCount = matches,
                MatchPct = matchPct
            });
            count++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Calculated {Count} breadth metrics.", count);
        return count;
    }

    private Dictionary<string, Dictionary<string, System.Text.Json.JsonElement>> GetLatestIndicatorValues(long tickerId)
    {
        return dbContext.IndicatorValues
            .Where(v => v.TickerId == tickerId)
            .Join(
                dbContext.IndicatorDefinitions,
                value => value.IndicatorDefinitionId,
                definition => definition.Id,
                (value, definition) => new { definition.Code, value })
            .GroupBy(x => x.Code)
            .Select(g => g.OrderByDescending(x => x.value.CandleDate).First())
            .ToDictionary(
                x => x.Code,
                x => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(x.value.ValuesJson)
                    ?? new Dictionary<string, System.Text.Json.JsonElement>());
    }

    private static bool Matches(
        Dictionary<string, System.Text.Json.JsonElement> values,
        string field,
        string op,
        object threshold)
    {
        if (!values.TryGetValue(field, out var element))
        {
            return false;
        }

        return op switch
        {
            "=" => CompareEqual(element, threshold),
            ">=" => element.ValueKind == System.Text.Json.JsonValueKind.Number && element.GetDecimal() >= Convert.ToDecimal(threshold),
            _ => false
        };
    }

    private static bool CompareEqual(System.Text.Json.JsonElement element, object threshold) => threshold switch
    {
        string s => element.ValueKind == System.Text.Json.JsonValueKind.String
            ? element.GetString() == s
            : element.ToString() == s,
        bool b => element.ValueKind == System.Text.Json.JsonValueKind.True ? b : element.ValueKind == System.Text.Json.JsonValueKind.False && !b,
        decimal d => element.ValueKind == System.Text.Json.JsonValueKind.Number && element.GetDecimal() == d,
        _ => false
    };
}
