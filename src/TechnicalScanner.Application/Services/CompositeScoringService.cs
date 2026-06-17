using Microsoft.Extensions.Logging;
using System.Text.Json;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Services;

public sealed class CompositeScoringService(
    ITechnicalScannerDbContext dbContext,
    ILogger<CompositeScoringService> logger)
{
    public async Task<int> CalculateAsync(long? scanRunId = null, CancellationToken cancellationToken = default)
    {
        var scanDate = DateTimeOffset.UtcNow;
        var tickers = dbContext.Tickers.Where(t => t.IsActive).ToList();
        var count = 0;

        foreach (var ticker in tickers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var values = GetLatestIndicatorValues(ticker.Id);
            var components = new Dictionary<string, decimal>();
            var score = 0m;

            if (GetString(values, "BMSB_W", "status") == "BelowBand")
            {
                components["belowBmsb"] = 2;
                score += 2;
            }

            if (GetDecimal(values, "RSI_14_D", "rsi") is < 30)
            {
                components["dailyRsiOversold"] = 1;
                score += 1;
            }

            if (GetDecimal(values, "RSI_14_W", "rsi") is < 40)
            {
                components["weeklyRsiWeak"] = 1;
                score += 1;
            }

            if (GetDecimal(values, "SMA_200_D", "distanceFromSmaPct") is < 0)
            {
                components["below200dSma"] = 1;
                score += 1;
            }

            if (GetDecimal(values, "SMA_50_W", "distanceFromSmaPct") is < 0)
            {
                components["below50wSma"] = 1;
                score += 1;
            }

            if (GetString(values, "BB_20_2_D", "status") == "CloseBelowLowerBand")
            {
                components["belowDailyBbLower"] = 1;
                score += 1;
            }

            var existing = dbContext.ScanScores.FirstOrDefault(s =>
                s.ScanDateUtc == scanDate && s.TickerId == ticker.Id && s.ScoreCode == "DEEP_PULLBACK");

            if (existing is null)
            {
                dbContext.Add(new ScanScore
                {
                    ScanRunId = scanRunId,
                    ScanDateUtc = scanDate,
                    TickerId = ticker.Id,
                    ScoreCode = "DEEP_PULLBACK",
                    Score = score,
                    ComponentsJson = JsonSerializer.Serialize(components)
                });
            }
            else
            {
                existing.ScanRunId = scanRunId;
                existing.Score = score;
                existing.ComponentsJson = JsonSerializer.Serialize(components);
            }

            count++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Calculated composite scores for {Count} tickers.", count);
        return count;
    }

    private Dictionary<string, Dictionary<string, JsonElement>> GetLatestIndicatorValues(long tickerId)
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
                x => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(x.value.ValuesJson)
                    ?? new Dictionary<string, JsonElement>());
    }

    private static string? GetString(Dictionary<string, Dictionary<string, JsonElement>> values, string code, string field)
    {
        if (!values.TryGetValue(code, out var indicator) || !indicator.TryGetValue(field, out var element))
        {
            return null;
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
    }

    private static decimal? GetDecimal(Dictionary<string, Dictionary<string, JsonElement>> values, string code, string field)
    {
        if (!values.TryGetValue(code, out var indicator) || !indicator.TryGetValue(field, out var element))
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetDecimal(),
            JsonValueKind.True => 1,
            JsonValueKind.False => 0,
            _ => decimal.TryParse(element.ToString(), out var parsed) ? parsed : null
        };
    }
}
