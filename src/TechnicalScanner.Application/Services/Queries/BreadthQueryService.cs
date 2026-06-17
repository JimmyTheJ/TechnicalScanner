using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Models.Queries;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Services.Queries;

public sealed class BreadthQueryService(ITechnicalScannerDbContext dbContext)
{
    public IReadOnlyList<BreadthSnapshot> GetLatest()
    {
        var latestScan = dbContext.BreadthSnapshots
            .OrderByDescending(b => b.ScanDateUtc)
            .Select(b => (DateTimeOffset?)b.ScanDateUtc)
            .FirstOrDefault();

        if (latestScan is null)
        {
            return [];
        }

        return dbContext.BreadthSnapshots
            .Where(b => b.ScanDateUtc == latestScan)
            .OrderBy(b => b.MetricCode)
            .ToList();
    }

    public IReadOnlyList<BreadthSnapshot> GetHistory(string metricCode, DateTimeOffset? from, DateTimeOffset? to)
    {
        var query = dbContext.BreadthSnapshots.Where(b => b.MetricCode == metricCode);

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
}
