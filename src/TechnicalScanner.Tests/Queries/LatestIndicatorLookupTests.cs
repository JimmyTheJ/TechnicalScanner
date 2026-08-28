using System.Text.Json;
using TechnicalScanner.Application.Services.Queries;

namespace TechnicalScanner.Tests.Queries;

public class LatestIndicatorLookupTests
{
    [Fact]
    public void Index_GroupsLatestSnapshotsByTickerAndCode()
    {
        var rows = new (long TickerId, string Code, string ValuesJson, decimal? PrimaryValue, decimal? SecondaryValue)[]
        {
            (1, "RSI_14_D", """{"rsi":20}""", 20m, null),
            (1, "SMA_200_D", """{"distanceFromSmaPct":-8}""", -8m, 100m),
            (2, "RSI_14_D", """{"rsi":55}""", 55m, null)
        };

        var lookup = LatestIndicatorLookup.Index(rows);

        Assert.Equal(20m, lookup[1]["RSI_14_D"].PrimaryValue);
        Assert.Equal(-8m, lookup[1]["SMA_200_D"].PrimaryValue);
        Assert.Equal(JsonValueKind.Number, lookup[1]["RSI_14_D"].Values.GetProperty("rsi").ValueKind);
        Assert.Equal(55m, lookup[2]["RSI_14_D"].PrimaryValue);
        Assert.False(lookup.ContainsKey(3));
    }
}
