using System.Text.Json;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Profiles;

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
        if (!TryResolve(dbContext, tickerIds, codes, out var tickerIdList, out var definitionIds))
        {
            return [];
        }

        return Index(Materialize(QueryLatestSnapshots(dbContext, tickerIdList, definitionIds)));
    }

    public static LatestPair LoadCurrentAndPrevious(
        ITechnicalScannerDbContext dbContext,
        IReadOnlyCollection<long> tickerIds,
        IReadOnlyCollection<string> codes)
    {
        if (!TryResolve(dbContext, tickerIds, codes, out var tickerIdList, out var definitionIds))
        {
            return new LatestPair([], []);
        }

        var current = Materialize(QueryLatestSnapshots(dbContext, tickerIdList, definitionIds));
        var previous = Materialize(QueryPreviousSnapshots(dbContext, tickerIdList, definitionIds));
        return new LatestPair(Index(current), Index(previous));
    }

    public static Dictionary<string, Dictionary<string, JsonElement>> ToEvaluatorMap(
        Dictionary<string, LatestIndicatorSnapshot>? snapshots)
    {
        var result = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase);
        if (snapshots is null)
        {
            return result;
        }

        foreach (var (code, snapshot) in snapshots)
        {
            result[code] = IndicatorValueMaps.ToFields(snapshot.Values);
        }

        return result;
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

    internal static IQueryable<IndicatorSnapshotRow> QueryLatestSnapshots(
        ITechnicalScannerDbContext dbContext,
        List<long> tickerIdList,
        List<long> definitionIds)
    {
        var latestDates = dbContext.IndicatorValues
            .Where(v => tickerIdList.Contains(v.TickerId) && definitionIds.Contains(v.IndicatorDefinitionId))
            .GroupBy(v => new { v.TickerId, v.IndicatorDefinitionId })
            .Select(g => new { g.Key.TickerId, g.Key.IndicatorDefinitionId, Date = g.Max(v => v.CandleDate) });

        return
            from value in dbContext.IndicatorValues
            join latest in latestDates
                on new { value.TickerId, value.IndicatorDefinitionId, value.CandleDate }
                equals new { latest.TickerId, latest.IndicatorDefinitionId, CandleDate = latest.Date }
            join definition in dbContext.IndicatorDefinitions on value.IndicatorDefinitionId equals definition.Id
            select new IndicatorSnapshotRow
            {
                TickerId = value.TickerId,
                Code = definition.Code,
                ValuesJson = value.ValuesJson,
                PrimaryValue = value.PrimaryValue,
                SecondaryValue = value.SecondaryValue
            };
    }

    internal static IQueryable<IndicatorSnapshotRow> QueryPreviousSnapshots(
        ITechnicalScannerDbContext dbContext,
        List<long> tickerIdList,
        List<long> definitionIds)
    {
        var latestDates = dbContext.IndicatorValues
            .Where(v => tickerIdList.Contains(v.TickerId) && definitionIds.Contains(v.IndicatorDefinitionId))
            .GroupBy(v => new { v.TickerId, v.IndicatorDefinitionId })
            .Select(g => new { g.Key.TickerId, g.Key.IndicatorDefinitionId, Date = g.Max(v => v.CandleDate) });

        var previousDates =
            from value in dbContext.IndicatorValues
            join latest in latestDates
                on new { value.TickerId, value.IndicatorDefinitionId }
                equals new { latest.TickerId, latest.IndicatorDefinitionId }
            where value.CandleDate < latest.Date
            group value by new { value.TickerId, value.IndicatorDefinitionId } into grouped
            select new { grouped.Key.TickerId, grouped.Key.IndicatorDefinitionId, Date = grouped.Max(v => v.CandleDate) };

        return
            from value in dbContext.IndicatorValues
            join previous in previousDates
                on new { value.TickerId, value.IndicatorDefinitionId, value.CandleDate }
                equals new { previous.TickerId, previous.IndicatorDefinitionId, CandleDate = previous.Date }
            join definition in dbContext.IndicatorDefinitions on value.IndicatorDefinitionId equals definition.Id
            select new IndicatorSnapshotRow
            {
                TickerId = value.TickerId,
                Code = definition.Code,
                ValuesJson = value.ValuesJson,
                PrimaryValue = value.PrimaryValue,
                SecondaryValue = value.SecondaryValue
            };
    }

    internal sealed class IndicatorSnapshotRow
    {
        public long TickerId { get; init; }
        public string Code { get; init; } = "";
        public string ValuesJson { get; init; } = "";
        public decimal? PrimaryValue { get; init; }
        public decimal? SecondaryValue { get; init; }
    }

    private static List<(long TickerId, string Code, string ValuesJson, decimal? PrimaryValue, decimal? SecondaryValue)> Materialize(
        IQueryable<IndicatorSnapshotRow> query)
    {
        return query
            .AsEnumerable()
            .Select(row => (row.TickerId, row.Code, row.ValuesJson, row.PrimaryValue, row.SecondaryValue))
            .ToList();
    }

    private static bool TryResolve(
        ITechnicalScannerDbContext dbContext,
        IReadOnlyCollection<long> tickerIds,
        IReadOnlyCollection<string> codes,
        out List<long> tickerIdList,
        out List<long> definitionIds)
    {
        tickerIdList = [];
        definitionIds = [];
        if (tickerIds.Count == 0 || codes.Count == 0)
        {
            return false;
        }

        tickerIdList = tickerIds as List<long> ?? tickerIds.ToList();
        var codeList = codes as List<string> ?? codes.ToList();
        var definitions = dbContext.IndicatorDefinitions
            .Where(d => codeList.Contains(d.Code))
            .Select(d => d.Id)
            .ToList();
        if (definitions.Count == 0)
        {
            return false;
        }

        definitionIds = definitions;
        return true;
    }
}
