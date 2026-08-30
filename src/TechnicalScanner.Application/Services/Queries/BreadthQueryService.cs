using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Profiles;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Services.Queries;

public sealed class BreadthQueryService(ITechnicalScannerDbContext dbContext)
{
    public IReadOnlyList<BreadthSnapshot> GetLatest(string? universe = null)
    {
        var latestScan = dbContext.BreadthSnapshots
            .OrderByDescending(b => b.ScanDateUtc)
            .Select(b => (DateTimeOffset?)b.ScanDateUtc)
            .FirstOrDefault();

        if (latestScan is null)
        {
            return [];
        }

        var query = dbContext.BreadthSnapshots.Where(b => b.ScanDateUtc == latestScan);
        var name = NormalizeUniverse(universe);
        if (name != BreadthAggregation.AllSlices)
        {
            query = query.Where(b => b.UniverseName == name);
        }

        return query.OrderBy(b => b.MetricCode).ToList();
    }

    public IReadOnlyList<BreadthSnapshot> GetHistory(
        string metricCode,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? universe = null)
    {
        var query = dbContext.BreadthSnapshots.Where(b => b.MetricCode == metricCode);
        var name = NormalizeUniverse(universe);
        if (name != BreadthAggregation.AllSlices)
        {
            query = query.Where(b => b.UniverseName == name);
        }

        if (from is not null)
        {
            query = query.Where(b => b.ScanDateUtc >= from);
        }

        if (to is not null)
        {
            query = query.Where(b => b.ScanDateUtc <= to);
        }

        return query.OrderBy(b => b.ScanDateUtc).ToList();
    }

    private static string NormalizeUniverse(string? universe)
    {
        if (string.IsNullOrWhiteSpace(universe))
        {
            return BreadthAggregation.AllUniverse;
        }

        var trimmed = universe.Trim();
        return trimmed == "*" ? BreadthAggregation.AllSlices : trimmed.ToUpperInvariant();
    }
}
