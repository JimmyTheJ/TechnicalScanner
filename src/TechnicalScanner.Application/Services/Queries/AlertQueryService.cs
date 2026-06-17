using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Services.Queries;

public sealed class AlertQueryService(ITechnicalScannerDbContext dbContext)
{
    public IReadOnlyList<AlertEvent> GetEvents(int page, int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        return dbContext.AlertEvents
            .OrderByDescending(e => e.DetectedUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    public AlertEvent? GetEvent(long id)
        => dbContext.AlertEvents.FirstOrDefault(e => e.Id == id);

    public IReadOnlyList<AlertDelivery> GetDeliveries(long alertEventId)
        => dbContext.AlertDeliveries
            .Where(d => d.AlertEventId == alertEventId)
            .OrderBy(d => d.Channel)
            .ToList();
}
