namespace TechnicalScanner.Domain.Entities;

public class AlertDelivery
{
    public long Id { get; set; }
    public long AlertEventId { get; set; }
    public required string Channel { get; set; }
    public required string Destination { get; set; }
    public required string Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset? AttemptedUtc { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public AlertEvent AlertEvent { get; set; } = null!;
}
