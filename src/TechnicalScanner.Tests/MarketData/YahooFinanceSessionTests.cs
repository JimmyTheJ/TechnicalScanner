using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using TechnicalScanner.Infrastructure.MarketData;

namespace TechnicalScanner.Tests.MarketData;

public class YahooFinanceSessionTests
{
    [Fact]
    public void IsValidCrumb_RejectsHtmlAndRateLimitBodies()
    {
        Assert.True(YahooFinanceSession.IsValidCrumb("abc.def"));
        Assert.False(YahooFinanceSession.IsValidCrumb(""));
        Assert.False(YahooFinanceSession.IsValidCrumb("Too Many Requests"));
        Assert.False(YahooFinanceSession.IsValidCrumb("<html>nope</html>"));
    }

    [Fact]
    public async Task GetChartAsync_WarmsCookiesFetchesCrumbAndSendsBrowserHeaders()
    {
        var handler = new ScriptedHandler
        {
            Respond = request =>
            {
                var url = request.RequestUri!.ToString();
                if (url.Contains("fc.yahoo.com", StringComparison.OrdinalIgnoreCase))
                {
                    return Text(HttpStatusCode.NotFound, string.Empty);
                }

                if (url.Contains("getcrumb", StringComparison.OrdinalIgnoreCase))
                {
                    return Text(HttpStatusCode.OK, "crumb-token");
                }

                if (url.Contains("/chart/AAMI", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Contains("crumb=crumb-token", url, StringComparison.Ordinal);
                    return Text(HttpStatusCode.OK, "{}");
                }

                return Text(HttpStatusCode.BadRequest, "unexpected");
            }
        };

        using var http = new HttpClient(handler);
        YahooFinanceSession.ConfigureClient(http);
        var session = new YahooFinanceSession(http, NullLogger<YahooFinanceSession>.Instance);

        using var response = await session.GetChartAsync(
            "https://query1.finance.yahoo.com/v8/finance/chart/AAMI?interval=1d",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, handler.Urls.Count);
        Assert.Contains("fc.yahoo.com", handler.Urls[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("getcrumb", handler.Urls[1], StringComparison.OrdinalIgnoreCase);
        Assert.All(handler.UserAgents, ua => Assert.Contains("Mozilla/5.0", ua, StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetChartAsync_RefreshesCrumbAfterUnauthorized()
    {
        var crumbCalls = 0;
        var chartCalls = 0;
        var handler = new ScriptedHandler
        {
            Respond = request =>
            {
                var url = request.RequestUri!.ToString();
                if (url.Contains("fc.yahoo.com", StringComparison.OrdinalIgnoreCase))
                {
                    return Text(HttpStatusCode.NotFound, string.Empty);
                }

                if (url.Contains("getcrumb", StringComparison.OrdinalIgnoreCase))
                {
                    crumbCalls++;
                    return Text(HttpStatusCode.OK, crumbCalls == 1 ? "old-crumb" : "new-crumb");
                }

                chartCalls++;
                if (url.Contains("old-crumb", StringComparison.Ordinal))
                {
                    return Text(HttpStatusCode.Unauthorized, "Invalid Crumb");
                }

                Assert.Contains("new-crumb", url, StringComparison.Ordinal);
                return Text(HttpStatusCode.OK, "{}");
            }
        };

        using var http = new HttpClient(handler);
        var session = new YahooFinanceSession(http, NullLogger<YahooFinanceSession>.Instance);

        using var response = await session.GetChartAsync(
            "https://query1.finance.yahoo.com/v8/finance/chart/AAMI?interval=1d",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, crumbCalls);
        Assert.Equal(2, chartCalls);
    }

    private static HttpResponseMessage Text(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body) };

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        public required Func<HttpRequestMessage, HttpResponseMessage> Respond { get; init; }

        public List<string> Urls { get; } = [];

        public List<string> UserAgents { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri!.ToString());
            UserAgents.Add(
                request.Headers.TryGetValues("User-Agent", out var values)
                    ? string.Join(" ", values)
                    : string.Empty);
            return Task.FromResult(Respond(request));
        }
    }
}
