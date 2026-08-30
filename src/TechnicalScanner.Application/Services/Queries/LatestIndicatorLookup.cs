using System.Text.Json;
using TechnicalScanner.Application.Abstractions;

namespace TechnicalScanner.Application.Services.Queries;

public sealed record LatestIndicatorSnapshot(JsonElement Values, decimal? PrimaryValue, decimal? SecondaryValue);

public sealed record LatestPair(
    Dictionary<long, Dictionary<string, LatestIndicatorSnapshot>> Current,
    Dictionary<long, Dictionary<string, LatestIndicatorSnapshot>> Previous);

public static class LatestIndicatorLookup
{
    public static Dictionary<long, Dictionary<string, LatestIndicatorSnapshot>> Load(
        ITechnicalScannerDbContext dbContext,
        IReadOnlyCollection<long> tickerIds,
        IReadOnlyCollection<string> codes)
    {
        var dates = LatestDates(dbContext, tickerIds, codes);
        return dates is null ? [] : Index(LoadAtDates(dbContext, dates));
    }

    public static LatestPair LoadCurrentAndPrevious(
        ITechnicalScannerDbContext dbContext,
        IReadOnlyCollection<long> tickerIds,
        IReadOnlyCollection<string> codes)
    {
        var latestDates = LatestDates(dbContext, tickerIds, codes);
        if (latestDates is null)
        {
            return new LatestPair([], []);
        }

        var current = LoadAtDates(dbContext, latestDates);
        var previousDates =
            from value in dbContext.IndicatorValues
            join latest in latestDates
                on new { value.TickerId, value.IndicatorDefinitionId }
                equals new { latest.TickerId, latest.IndicatorDefinitionId }
            where value.CandleDate < latest.Date
            group value by new { value.TickerId, value.IndicatorDefinitionId } into grouped
            select new LatestDateRow(grouped.Key.TickerId, grouped.Key.IndicatorDefinitionId, grouped.Max(v => v.CandleDate));
        var previous = LoadAtDates(dbContext, previousDates);

        return new LatestPair(Index(current), Index(previous));
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

    private static List<(long TickerId, string Code, string ValuesJson, decimal? PrimaryValue, decimal? SecondaryValue)> LoadAtDates(
        ITechnicalScannerDbContext dbContext,
        IQueryable<LatestDateRow> dates)
    {
        return (
            from value in dbContext.IndicatorValues
            join latest in dates
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
            }).AsEnumerable()
            .Select(row => (row.TickerId, row.Code, row.ValuesJson, row.PrimaryValue, row.SecondaryValue))
            .ToList();
    }

    private static IQueryable<LatestDateRow>? LatestDates(
        ITechnicalScannerDbContext dbContext,
        IReadOnlyCollection<long> tickerIds,
        IReadOnlyCollection<string> codes)
    {
        if (tickerIds.Count == 0 || codes.Count == 0)
        {
            return null;
        }

        var tickerIdList = tickerIds as List<long> ?? tickerIds.ToList();
        var codeList = codes as List<string> ?? codes.ToList();
        var definitions = dbContext.IndicatorDefinitions
            .Where(d => codeList.Contains(d.Code))
            .Select(d => new { d.Id, d.Code })
            .ToList();
        if (definitions.Count == 0)
        {
            return null;
        }

        var definitionIds = definitions.Select(d => d.Id).ToList();
        return dbContext.IndicatorValues
            .Where(v => tickerIdList.Contains(v.TickerId) && definitionIds.Contains(v.IndicatorDefinitionId))
            .GroupBy(v => new { v.TickerId, v.IndicatorDefinitionId })
            .Select(g => new LatestDateRow(g.Key.TickerId, g.Key.IndicatorDefinitionId, g.Max(v => v.CandleDate)));
    }

    private sealed record LatestDateRow(long TickerId, long IndicatorDefinitionId, DateOnly Date);
}
