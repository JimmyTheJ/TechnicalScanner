using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Models;
using TechnicalScanner.Application.Options;
using TechnicalScanner.Domain.Enums;

namespace TechnicalScanner.Infrastructure.MarketData;

public sealed class ResilientRateLimitedMarketDataProvider(
    IMarketDataProvider inner,
    IOptions<MarketDataOptions> options,
    ILogger<ResilientRateLimitedMarketDataProvider> logger) : IMarketDataProvider
{
    private readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private DateTimeOffset _nextRequestUtc = DateTimeOffset.MinValue;

    public Task<IReadOnlyList<TickerDto>> GetTickersAsync(CancellationToken cancellationToken)
        => ExecuteWithRetryAsync(
            () => inner.GetTickersAsync(cancellationToken),
            "get-tickers",
            cancellationToken);

    public Task<IReadOnlyList<PriceCandleDto>> GetHistoricalCandlesAsync(
        string symbol,
        string exchange,
        string country,
        Timeframe timeframe,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
        => ExecuteWithRetryAsync(
            () => inner.GetHistoricalCandlesAsync(symbol, exchange, country, timeframe, startDate, endDate, cancellationToken),
            $"get-candles:{symbol}",
            cancellationToken);

    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> action,
        string operation,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        Exception? lastException = null;

        for (var attempt = 1; attempt <= settings.MaxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await WaitForRateLimitAsync(settings.RequestsPerSecond, cancellationToken);
                return await action();
            }
            catch (Exception ex) when (attempt < settings.MaxRetries && IsTransient(ex))
            {
                lastException = ex;
                var delay = TimeSpan.FromSeconds(settings.RetryDelaySeconds * attempt);
                logger.LogWarning(
                    ex,
                    "Transient market data failure during {Operation}. Retry {Attempt}/{MaxRetries} in {DelaySeconds}s.",
                    operation,
                    attempt,
                    settings.MaxRetries,
                    delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
            }
        }

        throw lastException ?? new InvalidOperationException($"Market data operation failed: {operation}");
    }

    private async Task WaitForRateLimitAsync(double requestsPerSecond, CancellationToken cancellationToken)
    {
        if (requestsPerSecond <= 0)
        {
            return;
        }

        var minInterval = TimeSpan.FromSeconds(1.0 / requestsPerSecond);
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (_nextRequestUtc > now)
            {
                await Task.Delay(_nextRequestUtc - now, cancellationToken);
            }

            _nextRequestUtc = DateTimeOffset.UtcNow + minInterval;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    private static bool IsTransient(Exception ex)
        => ex is HttpRequestException or TaskCanceledException or TimeoutException;
}
