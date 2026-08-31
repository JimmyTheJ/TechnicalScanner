using TechnicalScanner.Infrastructure.MarketData;

namespace TechnicalScanner.Tests.MarketData;

public class MarketDataSymbolFormatterTests
{
    [Theory]
    [InlineData("RY", "TSX", "CA", "RY.TO")]
    [InlineData("SHOP", "TSXV", "CA", "SHOP.V")]
    [InlineData("ENB.PF.C", "TSX", "CA", "ENB.PF.C")]
    [InlineData("AAPL", "NASDAQ", "US", "AAPL")]
    [InlineData("BRK.B", "NYSE", "US", "BRK-B")]
    [InlineData("ACHR.W", "NYSE", "US", "ACHR-WT")]
    [InlineData("FOO.WS", "NYSE", "US", "FOO-WT")]
    [InlineData("BAR.U", "NYSE", "US", "BAR-UN")]
    [InlineData("BAZ.R", "NYSE", "US", "BAZ-RI")]
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
