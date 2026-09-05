using System.Net;
using Microsoft.Extensions.Logging;

namespace TechnicalScanner.Infrastructure.MarketData;

public sealed class YahooFinanceSession
{
    public const string HttpClientName = "YahooFinance";

    internal const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

    private const string CookieWarmupUrl = "https://fc.yahoo.com/";
    private const string CrumbUrl = "https://query1.finance.yahoo.com/v1/test/getcrumb";

    private readonly HttpClient _httpClient;
    private readonly ILogger<YahooFinanceSession> _logger;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);
    private string? _crumb;
    private bool _cookiesWarmed;

    public YahooFinanceSession(HttpClient httpClient, ILogger<YahooFinanceSession> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public static void ConfigureClient(HttpClient client)
    {
        client.Timeout = TimeSpan.FromMinutes(2);
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://finance.yahoo.com/");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Origin", "https://finance.yahoo.com");
    }

    public async Task<HttpResponseMessage> GetChartAsync(string chartUrl, CancellationToken cancellationToken)
    {
        var response = await SendChartAsync(chartUrl, forceRefresh: false, cancellationToken);
        if (response.StatusCode is not HttpStatusCode.Unauthorized and not HttpStatusCode.TooManyRequests)
        {
            return response;
        }

        var status = response.StatusCode;
        response.Dispose();
        _logger.LogInformation(
            "Yahoo chart request returned {StatusCode}; refreshing crumb and retrying.",
            (int)status);
        return await SendChartAsync(chartUrl, forceRefresh: true, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendChartAsync(
        string chartUrl,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        await EnsureSessionAsync(forceRefresh, cancellationToken);
        return await _httpClient.GetAsync(AppendCrumb(chartUrl), cancellationToken);
    }

    private async Task EnsureSessionAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        await _sessionLock.WaitAsync(cancellationToken);
        try
        {
            if (forceRefresh)
            {
                _crumb = null;
                _cookiesWarmed = false;
            }

            if (!_cookiesWarmed)
            {
                await WarmCookiesAsync(cancellationToken);
                _cookiesWarmed = true;
            }

            if (_crumb is null)
            {
                _crumb = await FetchCrumbAsync(cancellationToken);
            }
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private async Task WarmCookiesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(CookieWarmupUrl, cancellationToken);
            _logger.LogDebug("Yahoo cookie warmup returned {StatusCode}.", (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogDebug(ex, "Yahoo cookie warmup failed; continuing without fc.yahoo.com cookies.");
        }
    }

    private async Task<string?> FetchCrumbAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(CrumbUrl, cancellationToken);
            var body = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Yahoo crumb request returned {StatusCode}. Chart requests will continue without a crumb.",
                    (int)response.StatusCode);
                return null;
            }

            if (!IsValidCrumb(body))
            {
                _logger.LogWarning("Yahoo crumb response was not usable. Chart requests will continue without a crumb.");
                return null;
            }

            _logger.LogInformation("Obtained Yahoo Finance crumb for authenticated chart requests.");
            return body;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Yahoo crumb request failed. Chart requests will continue without a crumb.");
            return null;
        }
    }

    private string AppendCrumb(string chartUrl)
    {
        if (string.IsNullOrEmpty(_crumb))
        {
            return chartUrl;
        }

        var separator = chartUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return chartUrl + separator + "crumb=" + Uri.EscapeDataString(_crumb);
    }

    internal static bool IsValidCrumb(string crumb)
        => crumb.Length is > 0 and < 80
           && !crumb.Contains(' ', StringComparison.Ordinal)
           && !crumb.Contains('<', StringComparison.Ordinal)
           && !crumb.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase);
}
