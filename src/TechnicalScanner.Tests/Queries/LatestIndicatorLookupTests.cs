using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechnicalScanner.Application.Services.Queries;
using TechnicalScanner.Infrastructure.Persistence;

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

    [Fact]
    public void QueryLatestSnapshots_TranslatesForNpgsql()
    {
        using var db = CreateNpgsqlContext();

        var sql = LatestIndicatorLookup.QueryLatestSnapshots(db, [1L], [1L]).ToQueryString();

        Assert.Contains("indicator_value", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MAX(", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void QueryPreviousSnapshots_TranslatesForNpgsql()
    {
        using var db = CreateNpgsqlContext();

        var sql = LatestIndicatorLookup.QueryPreviousSnapshots(db, [1L], [1L]).ToQueryString();

        Assert.Contains("indicator_value", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MAX(", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static TechnicalScannerDbContext CreateNpgsqlContext()
        => new(new DbContextOptionsBuilder<TechnicalScannerDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=translation_check")
            .Options);
}
