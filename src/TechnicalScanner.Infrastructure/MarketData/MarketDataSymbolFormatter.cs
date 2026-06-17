namespace TechnicalScanner.Infrastructure.MarketData;

public static class MarketDataSymbolFormatter
{
    public static string ToProviderSymbol(string symbol, string exchange, string country)
    {
        if (country.Equals("CA", StringComparison.OrdinalIgnoreCase) && !symbol.Contains('.'))
        {
            return exchange.Equals("TSXV", StringComparison.OrdinalIgnoreCase)
                ? $"{symbol}.V"
                : $"{symbol}.TO";
        }

        return symbol;
    }

    public static (string Symbol, string Exchange, string Country) FromProviderSymbol(string providerSymbol)
    {
        if (providerSymbol.EndsWith(".TO", StringComparison.OrdinalIgnoreCase))
        {
            return (providerSymbol[..^3], "TSX", "CA");
        }

        if (providerSymbol.EndsWith(".V", StringComparison.OrdinalIgnoreCase))
        {
            return (providerSymbol[..^2], "TSXV", "CA");
        }

        return (providerSymbol, "US", "US");
    }
}
