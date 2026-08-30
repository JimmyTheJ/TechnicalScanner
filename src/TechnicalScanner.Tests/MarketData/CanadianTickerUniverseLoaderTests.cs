using System.Text.Json;
using TechnicalScanner.Infrastructure.MarketData.Universe;

namespace TechnicalScanner.Tests.MarketData;

public class CanadianTickerUniverseLoaderTests
{
    [Fact]
    public void EnumerateCompanies_ReadsResultsArray()
    {
        using var document = JsonDocument.Parse("""{"results":[{"symbol":"RY","name":"Royal Bank"}]}""");
        var companies = CanadianTickerUniverseLoader.EnumerateCompanies(document.RootElement).ToList();
        Assert.Single(companies);
        Assert.Equal("RY", companies[0].GetProperty("symbol").GetString());
    }

    [Fact]
    public void EnumerateCompanies_ReadsLegacyResultsCompanyObject()
    {
        using var document = JsonDocument.Parse("""{"results":{"company":[{"symbol":"SHOP"}]}}""");
        var companies = CanadianTickerUniverseLoader.EnumerateCompanies(document.RootElement).ToList();
        Assert.Single(companies);
        Assert.Equal("SHOP", companies[0].GetProperty("symbol").GetString());
    }
}
