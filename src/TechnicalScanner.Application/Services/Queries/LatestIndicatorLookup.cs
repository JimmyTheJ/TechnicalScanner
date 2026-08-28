using System.Text.Json;
using TechnicalScanner.Application.Abstractions;

namespace TechnicalScanner.Application.Services.Queries;

public sealed record LatestIndicatorSnapshot(JsonElement Values, decimal? PrimaryValue, decimal? SecondaryValue);

public static class LatestIndicatorLookup
{
    public static Dictionary<long, Dictionary<string, LatestIndicatorSnapshot>> Load(
        ITechnicalScannerDbContext dbContext,
        IReadOnlyCollection<long> tickerIds,
        IReadOnlyCollection<string> codes)
    {
        if (tickerIds.Count == 0 || codes.Count == 0)
        {
            return [];
        }

        var tickerIdList = tickerIds as List<long> ?? tickerIds.ToList();
        var codeList = codes as List<string> ?? codes.ToList();
        var definitions = dbContext.IndicatorDefinitions
            .Where(d => codeList.Contains(d.Code))
            .Select(d => new { d.Id, d.Code })
            .ToList();
        if (definitions.Count == 0)
        {
            return [];
        }

        var definitionIds = definitions.Select(d => d.Id).ToList();
        var latestDates = dbContext.IndicatorValues
            .Where(v => tickerIdList.Contains(v.TickerId) && definitionIds.Contains(v.IndicatorDefinitionId))
            .GroupBy(v => new { v.TickerId, v.IndicatorDefinitionId })
            .Select(g => new { g.Key.TickerId, g.Key.IndicatorDefinitionId, Date = g.Max(v => v.CandleDate) });

        var rows = (
            from value in dbContext.IndicatorValues
            join latest in latestDates
                on new { value.TickerId, value.IndicatorDefinitionId, value.CandleDate }
                equals new { latest.TickerId, latest.IndicatorDefinitionId, CandleDate = latest.Date }
            join definition in dbContext.IndicatorDefinitions on value.IndicatorDefinitionId equals definition.Id
            select new
            {
                value.TickerId,
                definition.Code,
                value.ValuesJson,
                value.PrimaryValue,
                value.SecondaryValue
            }).ToList();

        return Index(rows.Select(row => (
            row.TickerId,
            row.Code,
            row.ValuesJson,
            row.PrimaryValue,
            row.SecondaryValue)));
    }

    public static Dictionary<long, Dictionary<string, LatestIndicatorSnapshot>> Index(
        IEnumerable<(long TickerId, string Code, string ValuesJson, decimal? PrimaryValue, decimal? SecondaryValue)> rows)
    {
        return rows
            .GroupBy(row => row.TickerId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(
                    row => row.Code,
                    row => new LatestIndicatorSnapshot(
                        JsonSerializer.Deserialize<JsonElement>(row.ValuesJson),
                        row.PrimaryValue,
                        row.SecondaryValue),
                    StringComparer.OrdinalIgnoreCase));
    }
}
