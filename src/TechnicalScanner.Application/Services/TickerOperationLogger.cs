using Microsoft.Extensions.Logging;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Services;

public sealed class TickerOperationLogger(
    ITechnicalScannerDbContext dbContext,
    ILogger<TickerOperationLogger> logger) : ITickerOperationLogger
{
    public long? CurrentScanRunId { get; set; }

    public async Task LogFailureAsync(
        string symbol,
        string operation,
        string errorMessage,
        long? tickerId = null,
        CancellationToken cancellationToken = default)
    {
        dbContext.Add(new TickerOperationFailure
        {
            ScanRunId = CurrentScanRunId,
            TickerId = tickerId,
            Symbol = symbol,
            Operation = operation,
            ErrorMessage = errorMessage.Length > 4000 ? errorMessage[..4000] : errorMessage
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogWarning("Ticker operation failed for {Symbol} during {Operation}: {Error}", symbol, operation, errorMessage);
    }
}
