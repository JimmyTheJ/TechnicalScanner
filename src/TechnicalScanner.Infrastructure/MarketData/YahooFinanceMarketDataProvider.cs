using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Models;
using TechnicalScanner.Application.Options;
using TechnicalScanner.Domain.Enums;
using TechnicalScanner.Infrastructure.MarketData.Universe;

namespace TechnicalScanner.Infrastructure.MarketData;

public sealed class YahooFinanceMarketDataProvider(
    HttpClient httpClient,
    UsTickerUniverseLoader usTickerUniverseLoader,
    CanadianTickerUniverseLoader canadianTickerUniverseLoader,
    IOptions<MarketDataOptions> options,
    ILogger<YahooFinanceMarketDataProvider> logger) : IMarketDataProvider
{
    public async Task<IReadOnlyList<TickerDto>> GetTickersAsync(CancellationToken cancellationToken)
    {
        var universe = options.Value.Universe;
        var tickers = new List<TickerDto>();

        if (universe.IncludeUs)
        {
            tickers.AddRange(await usTickerUniverseLoader.LoadAsync(universe.IncludeEtfs, cancellationToken));
        }

        if (universe.IncludeCanada)
        {
            tickers.AddRange(await canadianTickerUniverseLoader.LoadAsync(universe.IncludeEtfs, cancellationToken));
        }

        logger.LogInformation("Loaded {Count} tickers from US/CA universe sources.", tickers.Count);
        return tickers;
    }

    public async Task<IReadOnlyList<PriceCandleDto>> GetHistoricalCandlesAsync(
        string symbol,
        string exchange,
        string country,
        Timeframe timeframe,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        if (timeframe != Timeframe.D)
        {
            return [];
        }

        var providerSymbol = MarketDataSymbolFormatter.ToProviderSymbol(symbol, exchange, country);
        var period1 = ToUnixSeconds(startDate);
        var period2 = ToUnixSeconds(endDate.AddDays(1));

        var url =
            $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(providerSymbol)}" +
            $"?interval=1d&period1={period1}&period2={period2}&includeAdjustedClose=true";

        using var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var result = document.RootElement
            .GetProperty("chart")
            .GetProperty("result");

        if (result.GetArrayLength() == 0)
        {
            return [];
        }

        var chart = result[0];
        if (!chart.TryGetProperty("timestamp", out var timestamps))
        {
            return [];
        }

        var quote = chart.GetProperty("indicators").GetProperty("quote")[0];
        var opens = quote.GetProperty("open");
        var highs = quote.GetProperty("high");
        var lows = quote.GetProperty("low");
        var closes = quote.GetProperty("close");
        var volumes = quote.GetProperty("volume");

        JsonElement adjCloses = default;
        var hasAdjClose = chart.GetProperty("indicators").TryGetProperty("adjclose", out var adjCloseContainer)
            && adjCloseContainer.GetArrayLength() > 0
            && adjCloseContainer[0].TryGetProperty("adjclose", out adjCloses);

        var candles = new List<PriceCandleDto>();
        for (var i = 0; i < timestamps.GetArrayLength(); i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (closes[i].ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            var close = closes[i].GetDecimal();
            if (close <= 0)
            {
                continue;
            }

            var candleDate = DateOnly.FromDateTime(
                DateTimeOffset.FromUnixTimeSeconds(timestamps[i].GetInt64()).UtcDateTime);

            candles.Add(new PriceCandleDto(
                candleDate,
                ReadNullableDecimal(opens, i),
                ReadNullableDecimal(highs, i),
                ReadNullableDecimal(lows, i),
                close,
                hasAdjClose ? ReadNullableDecimal(adjCloses, i) ?? close : close,
                ReadNullableLong(volumes, i),
                "yahoo"));
        }

        return candles;
    }

    private static long ToUnixSeconds(DateOnly date)
        => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).ToUnixTimeSeconds();

    private static decimal? ReadNullableDecimal(JsonElement array, int index)
        => array[index].ValueKind == JsonValueKind.Null ? null : array[index].GetDecimal();

    private static long? ReadNullableLong(JsonElement array, int index)
        => array[index].ValueKind == JsonValueKind.Null ? null : array[index].GetInt64();
}
