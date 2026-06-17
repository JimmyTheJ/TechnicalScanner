using System.Text.Json;
using Microsoft.Extensions.Logging;
using TechnicalScanner.Application.Models;

namespace TechnicalScanner.Infrastructure.MarketData.Universe;

public sealed class UsTickerUniverseLoader(HttpClient httpClient, ILogger<UsTickerUniverseLoader> logger)
{
    private const string NasdaqListedUrl = "https://www.nasdaqtrader.com/dynamic/SymDir/nasdaqlisted.txt";
    private const string OtherListedUrl = "https://www.nasdaqtrader.com/dynamic/SymDir/otherlisted.txt";

    public async Task<IReadOnlyList<TickerDto>> LoadAsync(bool includeEtfs, CancellationToken cancellationToken)
    {
        var tickers = new Dictionary<string, TickerDto>(StringComparer.OrdinalIgnoreCase);

        await ParseNasdaqListedAsync(tickers, includeEtfs, cancellationToken);
        await ParseOtherListedAsync(tickers, includeEtfs, cancellationToken);

        logger.LogInformation("Loaded {Count} US tickers.", tickers.Count);
        return tickers.Values.OrderBy(t => t.Symbol).ToList();
    }

    private async Task ParseNasdaqListedAsync(
        IDictionary<string, TickerDto> tickers,
        bool includeEtfs,
        CancellationToken cancellationToken)
    {
        var lines = await DownloadLinesAsync(NasdaqListedUrl, cancellationToken);
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("File Creation Time", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split('|');
            if (parts.Length < 7)
            {
                continue;
            }

            var symbol = parts[0].Trim();
            var name = parts[1].Trim();
            var isEtf = parts[6].Trim().Equals("Y", StringComparison.OrdinalIgnoreCase);
            var isTest = parts[3].Trim().Equals("Y", StringComparison.OrdinalIgnoreCase);

            if (!IsValidSymbol(symbol, isTest, isEtf, includeEtfs))
            {
                continue;
            }

            AddTicker(tickers, symbol, "NASDAQ", "US", name, isEtf);
        }
    }

    private async Task ParseOtherListedAsync(
        IDictionary<string, TickerDto> tickers,
        bool includeEtfs,
        CancellationToken cancellationToken)
    {
        var lines = await DownloadLinesAsync(OtherListedUrl, cancellationToken);
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("File Creation Time", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split('|');
            if (parts.Length < 8)
            {
                continue;
            }

            var symbol = parts[0].Trim();
            var name = parts[1].Trim();
            var exchange = MapUsExchange(parts[2].Trim());
            var isEtf = parts[4].Trim().Equals("Y", StringComparison.OrdinalIgnoreCase);
            var isTest = parts[6].Trim().Equals("Y", StringComparison.OrdinalIgnoreCase);

            if (!IsValidSymbol(symbol, isTest, isEtf, includeEtfs))
            {
                continue;
            }

            AddTicker(tickers, symbol, exchange, "US", name, isEtf);
        }
    }

    private async Task<string[]> DownloadLinesAsync(string url, CancellationToken cancellationToken)
    {
        var content = await httpClient.GetStringAsync(url, cancellationToken);
        return content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string MapUsExchange(string exchangeCode) => exchangeCode switch
    {
        "N" => "NYSE",
        "A" => "AMEX",
        "P" => "ARCA",
        "Z" => "BATS",
        "V" => "IEX",
        _ => exchangeCode
    };

    private static bool IsValidSymbol(string symbol, bool isTest, bool isEtf, bool includeEtfs)
    {
        if (isTest || string.IsNullOrWhiteSpace(symbol))
        {
            return false;
        }

        if (isEtf && !includeEtfs)
        {
            return false;
        }

        return symbol.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-');
    }

    private static void AddTicker(
        IDictionary<string, TickerDto> tickers,
        string symbol,
        string exchange,
        string country,
        string name,
        bool isEtf)
    {
        var key = $"{symbol}|{exchange}|{country}";
        tickers[key] = new TickerDto(symbol, exchange, country, "USD", name, null, null, null, isEtf);
    }
}
