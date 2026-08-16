using TechnicalScanner.Infrastructure.Configuration;

namespace TechnicalScanner.Tests.Configuration;

public class EnvFileParserTests
{
    [Fact]
    public void Parse_MapsDoubleUnderscoreKeysOntoAppsettingsPaths()
    {
        var parsed = EnvFileParser.Parse("""
            ConnectionStrings__TechnicalScanner=Host=localhost;Database=technical_scanner
            Alerts__Channels__Email__Password=secret
            """);

        Assert.Equal("Host=localhost;Database=technical_scanner", parsed["ConnectionStrings:TechnicalScanner"]);
        Assert.Equal("secret", parsed["Alerts:Channels:Email:Password"]);
    }

    [Fact]
    public void Parse_IgnoresCommentsBlankLinesAndExportPrefix()
    {
        var parsed = EnvFileParser.Parse("""
            # comment
            export Alerts__Enabled=true

            MarketData__Provider=Seed
            """);

        Assert.Equal("true", parsed["Alerts:Enabled"]);
        Assert.Equal("Seed", parsed["MarketData:Provider"]);
        Assert.Equal(2, parsed.Count);
    }

    [Fact]
    public void Parse_UnquotesValuesAndKeepsLastDuplicate()
    {
        var parsed = EnvFileParser.Parse("""
            Alerts__Channels__Email__Username="scanner@example.com"
            Alerts__Channels__Email__Username='override@example.com'
            """);

        Assert.Equal("override@example.com", parsed["Alerts:Channels:Email:Username"]);
    }
}
