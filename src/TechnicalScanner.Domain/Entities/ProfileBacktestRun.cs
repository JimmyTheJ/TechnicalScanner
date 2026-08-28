namespace TechnicalScanner.Domain.Entities;

public class ProfileBacktestRun
{
    public long Id { get; set; }
    public long ScanProfileId { get; set; }
    public required string ProfileCode { get; set; }
    public int ForwardDays { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public DateTimeOffset StartedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedUtc { get; set; }
    public required string Status { get; set; }
    public int TickerCount { get; set; }
    public int TickersWithEntries { get; set; }
    public int EntryCount { get; set; }
    public int MatchDays { get; set; }
    public decimal? MedianForwardReturnPct { get; set; }
    public decimal? MeanForwardReturnPct { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ProfileBacktestHit> Hits { get; set; } = [];
}
