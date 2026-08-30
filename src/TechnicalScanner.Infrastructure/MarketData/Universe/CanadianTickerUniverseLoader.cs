using System.Text.Json;
using Microsoft.Extensions.Logging;
using TechnicalScanner.Application.Models;

namespace TechnicalScanner.Infrastructure.MarketData.Universe;

public sealed class CanadianTickerUniverseLoader(HttpClient httpClient, ILogger<CanadianTickerUniverseLoader> logger)
{
    private const string TsxUrl = "https://www.tsx.com/json/company-directory/search/tsx/%5E";
    private const string TsxvUrl = "https://www.tsx.com/json/company-directory/search/tsx-v/%5E";

    public async Task<IReadOnlyList<TickerDto>> LoadAsync(bool includeEtfs, CancellationToken cancellationToken)
    {
        var tickers = new Dictionary<string, TickerDto>(StringComparer.OrdinalIgnoreCase);

        await LoadExchangeAsync(tickers, TsxUrl, "TSX", includeEtfs, cancellationToken);
        await LoadExchangeAsync(tickers, TsxvUrl, "TSXV", includeEtfs, cancellationToken);

        logger.LogInformation("Loaded {Count} Canadian tickers.", tickers.Count);
        return tickers.Values.OrderBy(t => t.Symbol).ToList();
    }

    private async Task LoadExchangeAsync(
        IDictionary<string, TickerDto> tickers,
        string url,
        string exchange,
        bool includeEtfs,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        foreach (var company in EnumerateCompanies(document.RootElement))
        {
            if (!company.TryGetProperty("symbol", out var symbolElement))
            {
                continue;
            }

            var symbol = symbolElement.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(symbol))
            {
                continue;
            }

            var name = company.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString()
                : symbol;

            var isEtf = name?.Contains("ETF", StringComparison.OrdinalIgnoreCase) == true
                || name?.Contains("Fund", StringComparison.OrdinalIgnoreCase) == true;

            if (isEtf && !includeEtfs)
            {
                continue;
            }

            var key = $"{symbol}|{exchange}|CA";
            tickers[key] = new TickerDto(symbol, exchange, "CA", "CAD", name, null, null, null, isEtf);
        }
    }

    /// <summary>
    /// TSX currently returns <c>{ "results": [ { "symbol", "name", ... } ] }</c>.
    /// Older payloads used <c>{ "results": { "company": [ ... ] } }</c>.
    /// </summary>
    public static IEnumerable<JsonElement> EnumerateCompanies(JsonElement root)
    {
        JsonElement results;
        if (root.ValueKind == JsonValueKind.Array)
        {
            results = root;
        }
        else if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("results", out results))
        {
            yield break;
        }

        if (results.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in results.EnumerateArray())
            {
                yield return item;
            }

            yield break;
        }

        if (results.ValueKind == JsonValueKind.Object
            && results.TryGetProperty("company", out var companies)
            && companies.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in companies.EnumerateArray())
            {
                yield return item;
            }
        }
    }
}
