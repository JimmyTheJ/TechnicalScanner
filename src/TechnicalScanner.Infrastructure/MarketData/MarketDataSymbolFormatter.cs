namespace TechnicalScanner.Infrastructure.MarketData;

public static class MarketDataSymbolFormatter
{
    public static string ToProviderSymbol(string symbol, string exchange, string country)
    {
        if (country.Equals("CA", StringComparison.OrdinalIgnoreCase))
        {
            if (symbol.Contains('.', StringComparison.Ordinal))
            {
                return symbol;
            }

            return exchange.Equals("TSXV", StringComparison.OrdinalIgnoreCase)
                ? $"{symbol}.V"
                : $"{symbol}.TO";
        }

        return ToYahooUsSymbol(symbol);
    }

    internal static string ToYahooUsSymbol(string symbol)
    {
        if (!symbol.Contains('.', StringComparison.Ordinal))
        {
            return symbol;
        }

        // NASDAQ/NYSE listed files use dots (ACHR.W, BRK.B). Yahoo uses dashes,
        // and warrants use -WT rather than -W.
        if (symbol.EndsWith(".WS", StringComparison.OrdinalIgnoreCase)
            || symbol.EndsWith(".W", StringComparison.OrdinalIgnoreCase))
        {
            var root = symbol[..symbol.LastIndexOf('.')];
            return root + "-WT";
        }

        if (symbol.EndsWith(".U", StringComparison.OrdinalIgnoreCase))
        {
            return symbol[..^2] + "-UN";
        }

        if (symbol.EndsWith(".R", StringComparison.OrdinalIgnoreCase))
        {
            return symbol[..^2] + "-RI";
        }

        return symbol.Replace('.', '-');
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
