namespace TechnicalScanner.Domain.Entities;

public class AlertEvent
{
    public long Id { get; set; }
    public long? ScanRunId { get; set; }
    public required string EventType { get; set; }
    public required string SubjectType { get; set; }
    public required string SubjectKey { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
    public string? DetailsJson { get; set; }
    public DateTimeOffset DetectedUtc { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public ScanRun? ScanRun { get; set; }
    public ICollection<AlertDelivery> Deliveries { get; set; } = [];
}
