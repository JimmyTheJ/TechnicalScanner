using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Models;
using TechnicalScanner.Application.Options;
using TechnicalScanner.Domain.Enums;

namespace TechnicalScanner.Infrastructure.MarketData;

public sealed class ResilientRateLimitedMarketDataProvider : IMarketDataProvider
{
    private readonly IMarketDataProvider _inner;
    private readonly IOptions<MarketDataOptions> _options;
    private readonly ILogger<ResilientRateLimitedMarketDataProvider> _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private DateTimeOffset _nextRequestUtc = DateTimeOffset.MinValue;
    private int _consecutiveTooManyRequests;

    public ResilientRateLimitedMarketDataProvider(
        IMarketDataProvider inner,
        IOptions<MarketDataOptions> options,
        ILogger<ResilientRateLimitedMarketDataProvider> logger,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        _inner = inner;
        _options = options;
        _logger = logger;
        _delayAsync = delayAsync ?? ((delay, cancellationToken) => Task.Delay(delay, cancellationToken));
    }

    public Task<IReadOnlyList<TickerDto>> GetTickersAsync(CancellationToken cancellationToken)
        => ExecuteWithRetryAsync(
            () => _inner.GetTickersAsync(cancellationToken),
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
            () => _inner.GetHistoricalCandlesAsync(symbol, exchange, country, timeframe, startDate, endDate, cancellationToken),
            $"get-candles:{symbol}",
            cancellationToken);

    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> action,
        string operation,
        CancellationToken cancellationToken)
    {
        var settings = _options.Value;
        var attempt = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempt++;

            try
            {
                await WaitForRateLimitAsync(settings.RequestsPerSecond, cancellationToken);
                var result = await action();
                ResetTooManyRequestsBackoff();
                return result;
            }
            catch (Exception ex) when (IsTooManyRequests(ex))
            {
                var consecutive = Interlocked.Increment(ref _consecutiveTooManyRequests);
                var maxConsecutive = settings.TooManyRequestsMaxConsecutive;
                if (maxConsecutive > 0 && consecutive >= maxConsecutive)
                {
                    _logger.LogError(
                        ex,
                        "Market data rate limited (429) during {Operation}. Consecutive 429s: {Consecutive} reached the maximum of {Max}. Giving up on this request.",
                        operation,
                        consecutive,
                        maxConsecutive);
                    throw new MarketDataRateLimitedException(operation, consecutive, ex);
                }

                var delay = MarketDataBackoff.TooManyRequestsDelay(
                    consecutive,
                    settings.TooManyRequestsDelayMinutes,
                    settings.TooManyRequestsAdditionalMinutes,
                    settings.TooManyRequestsMultiplier);
                _logger.LogWarning(
                    ex,
                    "Market data rate limited (429) during {Operation}. Consecutive 429s: {Consecutive}. Waiting {DelayMinutes} minutes before retrying.",
                    operation,
                    consecutive,
                    delay.TotalMinutes);

                await _delayAsync(delay, cancellationToken);
            }
            catch (Exception ex) when (attempt < settings.MaxRetries && IsTransient(ex))
            {
                var delay = TimeSpan.FromSeconds(settings.RetryDelaySeconds * attempt);
                _logger.LogWarning(
                    ex,
                    "Transient market data failure during {Operation}. Retry {Attempt}/{MaxRetries} in {DelaySeconds}s.",
                    operation,
                    attempt,
                    settings.MaxRetries,
                    delay.TotalSeconds);

                await _delayAsync(delay, cancellationToken);
            }
        }
    }

    private void ResetTooManyRequestsBackoff()
        => Interlocked.Exchange(ref _consecutiveTooManyRequests, 0);

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
                await _delayAsync(_nextRequestUtc - now, cancellationToken);
            }

            _nextRequestUtc = DateTimeOffset.UtcNow + minInterval;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    private static bool IsTooManyRequests(Exception ex)
        => ex is HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests };

    private static bool IsTransient(Exception ex)
    {
        if (ex is TaskCanceledException or TimeoutException)
        {
            return true;
        }

        if (ex is not HttpRequestException http)
        {
            return false;
        }

        // Network failures have no status. Retry 408 and 5xx only — 404 is a missing symbol.
        if (http.StatusCode is null)
        {
            return true;
        }

        var status = (int)http.StatusCode.Value;
        return status == 408 || status >= 500;
    }
}

internal static class MarketDataBackoff
{
    public static TimeSpan TooManyRequestsDelay(
        int consecutiveFailures,
        double baseDelayMinutes,
        double additionalMinutes,
        double multiplier)
    {
        if (consecutiveFailures <= 0)
        {
            return TimeSpan.Zero;
        }

        var baseMinutes = Math.Max(0, baseDelayMinutes);
        var addition = Math.Max(0, additionalMinutes);
        var factor = multiplier < 0 ? 1 : multiplier;

        // First 429 uses the base wait only; the multiplier is not applied.
        var delayMinutes = baseMinutes;
        for (var attempt = 2; attempt <= consecutiveFailures; attempt++)
        {
            var accruedAdditional = Math.Max(0, delayMinutes - baseMinutes);
            delayMinutes = (baseMinutes + accruedAdditional + addition) * factor;
        }

        return TimeSpan.FromMinutes(delayMinutes);
    }
}
