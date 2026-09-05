using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Models;
using TechnicalScanner.Application.Options;
using TechnicalScanner.Domain.Enums;
using TechnicalScanner.Infrastructure.MarketData;

namespace TechnicalScanner.Tests.MarketData;

public class ResilientRateLimitedMarketDataProviderTests
{
    [Theory]
    [InlineData(1, 30, 30, 1, 30)]
    [InlineData(2, 30, 30, 1, 60)]
    [InlineData(3, 30, 30, 1, 90)]
    [InlineData(4, 30, 30, 1, 120)]
    [InlineData(1, 30, 10, 1.5, 30)]
    [InlineData(2, 30, 10, 1.5, 60)]
    [InlineData(3, 30, 10, 1.5, 105)]
    [InlineData(4, 30, 10, 1.5, 172.5)]
    [InlineData(2, 30, 30, 1.5, 90)]
    [InlineData(2, 30, 30, 0.5, 30)]
    [InlineData(4, 30, 30, 0, 0)]
    public void TooManyRequestsDelay_AppliesBaseAccruedAdditionAndMultiplier(
        int consecutive,
        double baseMinutes,
        double additionalMinutes,
        double multiplier,
        double expectedMinutes)
    {
        Assert.Equal(
            TimeSpan.FromMinutes(expectedMinutes),
            MarketDataBackoff.TooManyRequestsDelay(consecutive, baseMinutes, additionalMinutes, multiplier));
    }

    [Fact]
    public void TooManyRequestsDelay_ClampsNegativeMultiplierToOne()
    {
        Assert.Equal(
            TimeSpan.FromMinutes(120),
            MarketDataBackoff.TooManyRequestsDelay(4, 30, 30, -1.5));
    }

    [Fact]
    public async Task GetHistoricalCandles_WaitsIncreasingCooldownsOnConsecutive429s_ThenResetsAfterSuccess()
    {
        var delays = new List<TimeSpan>();
        var remainingFailures = 2;
        var inner = new StubMarketDataProvider(() =>
        {
            if (remainingFailures > 0)
            {
                remainingFailures--;
                throw new HttpRequestException("Too Many Requests", null, HttpStatusCode.TooManyRequests);
            }

            return [];
        });

        var provider = CreateProvider(inner, delays);

        await provider.GetHistoricalCandlesAsync("CHE", "NYSE", "US", Timeframe.D, new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31), CancellationToken.None);

        Assert.Equal(
            [TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(60)],
            delays.Where(d => d >= TimeSpan.FromMinutes(30)).ToArray());

        remainingFailures = 1;
        delays.Clear();

        await provider.GetHistoricalCandlesAsync("CHE", "NYSE", "US", Timeframe.D, new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31), CancellationToken.None);

        Assert.Contains(TimeSpan.FromMinutes(30), delays);
        Assert.DoesNotContain(TimeSpan.FromMinutes(60), delays);
    }

    [Fact]
    public async Task GetHistoricalCandles_AppliesMultiplierOnFourth429()
    {
        var delays = new List<TimeSpan>();
        var remainingFailures = 4;
        var inner = new StubMarketDataProvider(() =>
        {
            if (remainingFailures > 0)
            {
                remainingFailures--;
                throw new HttpRequestException("Too Many Requests", null, HttpStatusCode.TooManyRequests);
            }

            return [];
        });

        var options = new MarketDataOptions
        {
            RequestsPerSecond = 0,
            MaxRetries = 2,
            TooManyRequestsDelayMinutes = 30,
            TooManyRequestsAdditionalMinutes = 10,
            TooManyRequestsMultiplier = 1.5,
            TooManyRequestsMaxConsecutive = 8
        };

        var provider = CreateProvider(inner, delays, options);

        await provider.GetHistoricalCandlesAsync("CHE", "NYSE", "US", Timeframe.D, new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31), CancellationToken.None);

        Assert.Equal(
            [
                TimeSpan.FromMinutes(30),
                TimeSpan.FromMinutes(60),
                TimeSpan.FromMinutes(105),
                TimeSpan.FromMinutes(172.5)
            ],
            delays);
    }

    [Fact]
    public async Task GetHistoricalCandles_DoesNotCount429AgainstMaxRetries()
    {
        var calls = 0;
        var inner = new StubMarketDataProvider(() =>
        {
            calls++;
            if (calls < 4)
            {
                throw new HttpRequestException("Too Many Requests", null, HttpStatusCode.TooManyRequests);
            }

            return [];
        });

        var options = new MarketDataOptions
        {
            RequestsPerSecond = 0,
            MaxRetries = 2,
            TooManyRequestsDelayMinutes = 30,
            TooManyRequestsAdditionalMinutes = 30,
            TooManyRequestsMultiplier = 1,
            TooManyRequestsMaxConsecutive = 8
        };

        var provider = CreateProvider(inner, [], options);

        await provider.GetHistoricalCandlesAsync("CHE", "NYSE", "US", Timeframe.D, new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31), CancellationToken.None);

        Assert.Equal(4, calls);
    }

    [Fact]
    public async Task GetHistoricalCandles_DoesNotRetryNotFound()
    {
        var calls = 0;
        var delays = new List<TimeSpan>();
        var inner = new StubMarketDataProvider(() =>
        {
            calls++;
            throw new HttpRequestException("Not Found", null, HttpStatusCode.NotFound);
        });

        var provider = CreateProvider(inner, delays, new MarketDataOptions
        {
            RequestsPerSecond = 0,
            MaxRetries = 5,
            RetryDelaySeconds = 45,
            TooManyRequestsMaxConsecutive = 8
        });

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            provider.GetHistoricalCandlesAsync(
                "ACHR.W",
                "NYSE",
                "US",
                Timeframe.D,
                new DateOnly(2024, 1, 1),
                new DateOnly(2024, 1, 31),
                CancellationToken.None));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Equal(1, calls);
        Assert.Empty(delays);
    }

    [Fact]
    public async Task GetHistoricalCandles_GivesUpAfterMaxConsecutive429s()
    {
        var calls = 0;
        var delays = new List<TimeSpan>();
        var inner = new StubMarketDataProvider(() =>
        {
            calls++;
            throw new HttpRequestException("Too Many Requests", null, HttpStatusCode.TooManyRequests);
        });

        var options = new MarketDataOptions
        {
            RequestsPerSecond = 0,
            MaxRetries = 8,
            TooManyRequestsDelayMinutes = 30,
            TooManyRequestsAdditionalMinutes = 30,
            TooManyRequestsMultiplier = 1,
            TooManyRequestsMaxConsecutive = 2
        };

        var provider = CreateProvider(inner, delays, options);

        var ex = await Assert.ThrowsAsync<MarketDataRateLimitedException>(() =>
            provider.GetHistoricalCandlesAsync(
                "MPCT.DB",
                "TSX",
                "CA",
                Timeframe.D,
                new DateOnly(2024, 1, 1),
                new DateOnly(2024, 1, 31),
                CancellationToken.None));

        Assert.Equal(2, calls);
        Assert.Equal(2, ex.ConsecutiveFailures);
        Assert.Equal([TimeSpan.FromMinutes(30)], delays);
    }

    [Fact]
    public async Task GetHistoricalCandles_DoesNotWaitOnThe429ThatOpensTheCircuit()
    {
        var calls = 0;
        var delays = new List<TimeSpan>();
        var inner = new StubMarketDataProvider(() =>
        {
            calls++;
            throw new HttpRequestException("Too Many Requests", null, HttpStatusCode.TooManyRequests);
        });

        var options = new MarketDataOptions
        {
            RequestsPerSecond = 0,
            TooManyRequestsDelayMinutes = 60,
            TooManyRequestsAdditionalMinutes = 0,
            TooManyRequestsMultiplier = 2,
            TooManyRequestsMaxConsecutive = 1
        };

        var provider = CreateProvider(inner, delays, options);

        await Assert.ThrowsAsync<MarketDataRateLimitedException>(() =>
            provider.GetHistoricalCandlesAsync(
                "MPCT.DB",
                "TSX",
                "CA",
                Timeframe.D,
                new DateOnly(2024, 1, 1),
                new DateOnly(2024, 1, 31),
                CancellationToken.None));

        Assert.Equal(1, calls);
        Assert.Empty(delays);
    }

    private static ResilientRateLimitedMarketDataProvider CreateProvider(
        IMarketDataProvider inner,
        List<TimeSpan> delays,
        MarketDataOptions? options = null)
    {
        options ??= new MarketDataOptions
        {
            RequestsPerSecond = 0,
            MaxRetries = 3,
            TooManyRequestsDelayMinutes = 30,
            TooManyRequestsAdditionalMinutes = 30,
            TooManyRequestsMultiplier = 1,
            TooManyRequestsMaxConsecutive = 8
        };

        return new ResilientRateLimitedMarketDataProvider(
            inner,
            Options.Create(options),
            NullLogger<ResilientRateLimitedMarketDataProvider>.Instance,
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });
    }

    private sealed class StubMarketDataProvider(Func<IReadOnlyList<PriceCandleDto>> candles) : IMarketDataProvider
    {
        public Task<IReadOnlyList<TickerDto>> GetTickersAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<TickerDto>>([]);

        public Task<IReadOnlyList<PriceCandleDto>> GetHistoricalCandlesAsync(
            string symbol,
            string exchange,
            string country,
            Timeframe timeframe,
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken)
            => Task.FromResult(candles());
    }
}
