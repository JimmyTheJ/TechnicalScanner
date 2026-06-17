using TechnicalScanner.Infrastructure.MarketData;

namespace TechnicalScanner.Tests.MarketData;

public class MarketDataSymbolFormatterTests
{
    [Theory]
    [InlineData("RY", "TSX", "CA", "RY.TO")]
    [InlineData("SHOP", "TSXV", "CA", "SHOP.V")]
    [InlineData("AAPL", "NASDAQ", "US", "AAPL")]
    public void ToProviderSymbol_FormatsCanadianAndUsSymbols(string symbol, string exchange, string country, string expected)
    {
        Assert.Equal(expected, MarketDataSymbolFormatter.ToProviderSymbol(symbol, exchange, country));
    }

    [Theory]
    [InlineData("RY.TO", "RY", "TSX", "CA")]
    [InlineData("SHOP.V", "SHOP", "TSXV", "CA")]
    [InlineData("AAPL", "AAPL", "US", "US")]
    public void FromProviderSymbol_ParsesCanadianAndUsSymbols(string providerSymbol, string symbol, string exchange, string country)
    {
        var parsed = MarketDataSymbolFormatter.FromProviderSymbol(providerSymbol);
        Assert.Equal(symbol, parsed.Symbol);
        Assert.Equal(exchange, parsed.Exchange);
        Assert.Equal(country, parsed.Country);
    }
}
