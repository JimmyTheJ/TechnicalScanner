using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Options;
using TechnicalScanner.Domain.Entities;
using TechnicalScanner.Domain.Enums;

namespace TechnicalScanner.Application.Services;

public sealed class NightlyScanService(
    ITechnicalScannerDbContext dbContext,
    TickerImportService tickerImportService,
    PriceImportService priceImportService,
    CandleAggregationService candleAggregationService,
    IndicatorCalculationService indicatorCalculationService,
    SignalEvaluationService signalEvaluationService,
    CompositeScoringService compositeScoringService,
    BreadthCalculationService breadthCalculationService,
    CsvExportService csvExportService,
    ITickerOperationLogger tickerOperationLogger,
    IOptions<MarketDataOptions> marketDataOptions,
    ILogger<NightlyScanService> logger)
{
    public async Task<ScanRun> RunAsync(
        bool incrementalPrices,
        int? historyYears,
        string? exportPath,
        CancellationToken cancellationToken)
    {
        var scanRun = new ScanRun
        {
            StartedUtc = DateTimeOffset.UtcNow,
            Status = "Running"
        };

        dbContext.Add(scanRun);
        await dbContext.SaveChangesAsync(cancellationToken);
        tickerOperationLogger.CurrentScanRunId = scanRun.Id;

        var summary = new Dictionary<string, object>();
        var failures = 0;

        try
        {
            var imported = await tickerImportService.ImportAsync(cancellationToken);
            summary["tickersImported"] = imported;

            var years = historyYears ?? marketDataOptions.Value.InitialHistoryYears;
            var pricesUpdated = await priceImportService.UpdatePricesAsync(years, incrementalPrices, cancellationToken);
            summary["pricesUpdated"] = pricesUpdated;

            var aggregated = await candleAggregationService.AggregateAsync([Timeframe.W, Timeframe.M], cancellationToken);
            summary["candlesAggregated"] = aggregated;

            var indicatorsCalculated = await indicatorCalculationService.CalculateAsync(cancellationToken);
            summary["indicatorsCalculated"] = indicatorsCalculated;

            var signalsEvaluated = await signalEvaluationService.EvaluateAsync(cancellationToken);
            summary["signalsEvaluated"] = signalsEvaluated;

            var scoresCalculated = await compositeScoringService.CalculateAsync(cancellationToken);
            summary["scoresCalculated"] = scoresCalculated;

            var breadthCalculated = await breadthCalculationService.CalculateAsync(cancellationToken);
            summary["breadthCalculated"] = breadthCalculated;

            if (!string.IsNullOrWhiteSpace(exportPath))
            {
                var path = await csvExportService.ExportLatestIndicatorsAsync(exportPath, cancellationToken);
                summary["exportPath"] = path;
            }

            failures = dbContext.TickerOperationFailures.Count(f => f.ScanRunId == scanRun.Id);
            scanRun.Status = "Completed";
            scanRun.TickersProcessed = pricesUpdated;
            scanRun.TickersFailed = failures;
            scanRun.SummaryJson = JsonSerializer.Serialize(summary);
            scanRun.CompletedUtc = DateTimeOffset.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Nightly scan {ScanRunId} completed. Prices updated: {Updated}, failures: {Failures}.",
                scanRun.Id,
                pricesUpdated,
                failures);

            return scanRun;
        }
        catch (Exception ex)
        {
            scanRun.Status = "Failed";
            scanRun.CompletedUtc = DateTimeOffset.UtcNow;
            summary["error"] = ex.Message;
            scanRun.SummaryJson = JsonSerializer.Serialize(summary);
            scanRun.TickersFailed = dbContext.TickerOperationFailures.Count(f => f.ScanRunId == scanRun.Id);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogError(ex, "Nightly scan {ScanRunId} failed.", scanRun.Id);
            throw;
        }
        finally
        {
            tickerOperationLogger.CurrentScanRunId = null;
        }
    }
}
