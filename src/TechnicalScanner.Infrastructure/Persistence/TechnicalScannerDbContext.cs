using Microsoft.EntityFrameworkCore;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Infrastructure.Persistence;

public sealed class TechnicalScannerDbContext(DbContextOptions<TechnicalScannerDbContext> options)
    : DbContext(options), ITechnicalScannerDbContext
{
    public DbSet<Ticker> Tickers => Set<Ticker>();
    public DbSet<PriceCandle> PriceCandles => Set<PriceCandle>();
    public DbSet<IndicatorDefinition> IndicatorDefinitions => Set<IndicatorDefinition>();
    public DbSet<IndicatorValue> IndicatorValues => Set<IndicatorValue>();
    public DbSet<SignalDefinition> SignalDefinitions => Set<SignalDefinition>();
    public DbSet<SignalResult> SignalResults => Set<SignalResult>();
    public DbSet<ScanScore> ScanScores => Set<ScanScore>();
    public DbSet<BreadthSnapshot> BreadthSnapshots => Set<BreadthSnapshot>();
    public DbSet<ScanRun> ScanRuns => Set<ScanRun>();
    public DbSet<TickerOperationFailure> TickerOperationFailures => Set<TickerOperationFailure>();

    IQueryable<Ticker> ITechnicalScannerDbContext.Tickers => Tickers;
    IQueryable<PriceCandle> ITechnicalScannerDbContext.PriceCandles => PriceCandles;
    IQueryable<IndicatorDefinition> ITechnicalScannerDbContext.IndicatorDefinitions => IndicatorDefinitions;
    IQueryable<IndicatorValue> ITechnicalScannerDbContext.IndicatorValues => IndicatorValues;
    IQueryable<SignalDefinition> ITechnicalScannerDbContext.SignalDefinitions => SignalDefinitions;
    IQueryable<SignalResult> ITechnicalScannerDbContext.SignalResults => SignalResults;
    IQueryable<ScanScore> ITechnicalScannerDbContext.ScanScores => ScanScores;
    IQueryable<BreadthSnapshot> ITechnicalScannerDbContext.BreadthSnapshots => BreadthSnapshots;
    IQueryable<ScanRun> ITechnicalScannerDbContext.ScanRuns => ScanRuns;
    IQueryable<TickerOperationFailure> ITechnicalScannerDbContext.TickerOperationFailures => TickerOperationFailures;

    public new void Add<TEntity>(TEntity entity) where TEntity : class => Set<TEntity>().Add(entity);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TechnicalScannerDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
