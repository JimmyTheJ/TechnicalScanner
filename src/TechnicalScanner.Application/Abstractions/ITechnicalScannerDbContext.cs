using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Abstractions;

public interface ITechnicalScannerDbContext
{
    IQueryable<Ticker> Tickers { get; }
    IQueryable<PriceCandle> PriceCandles { get; }
    IQueryable<IndicatorDefinition> IndicatorDefinitions { get; }
    IQueryable<IndicatorValue> IndicatorValues { get; }
    IQueryable<SignalDefinition> SignalDefinitions { get; }
    IQueryable<SignalResult> SignalResults { get; }
    IQueryable<ScanScore> ScanScores { get; }
    IQueryable<BreadthSnapshot> BreadthSnapshots { get; }
    IQueryable<ScanRun> ScanRuns { get; }
    IQueryable<TickerOperationFailure> TickerOperationFailures { get; }
    IQueryable<AlertEvent> AlertEvents { get; }
    IQueryable<AlertDelivery> AlertDeliveries { get; }

    void Add<TEntity>(TEntity entity) where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
