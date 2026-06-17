namespace TechnicalScanner.Application.Abstractions;

public interface ITickerOperationLogger
{
    long? CurrentScanRunId { get; set; }

    Task LogFailureAsync(
        string symbol,
        string operation,
        string errorMessage,
        long? tickerId = null,
        CancellationToken cancellationToken = default);
}
