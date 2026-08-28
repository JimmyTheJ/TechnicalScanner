namespace TechnicalScanner.Domain.Entities;

public class ProfileBacktestHit
{
    public long Id { get; set; }
    public long ProfileBacktestRunId { get; set; }
    public long TickerId { get; set; }
    public required string Symbol { get; set; }
    public DateOnly EntryDate { get; set; }
    public decimal Close { get; set; }
    public decimal? ForwardReturnPct { get; set; }
    public decimal? Score { get; set; }

    public ProfileBacktestRun Run { get; set; } = null!;
}
