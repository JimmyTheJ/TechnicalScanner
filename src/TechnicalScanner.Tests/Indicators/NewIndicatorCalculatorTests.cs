using TechnicalScanner.Application.Indicators.Calculators;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Tests.Indicators;

public class NewIndicatorCalculatorTests
{
    [Fact]
    public async Task VolumeCalculator_ComputesRelativeVolume()
    {
        var candles = Enumerable.Range(0, 5)
            .Select(i => Candle(i, 10m, volume: i == 4 ? 200 : 100))
            .ToList();
        var calculator = new VolumeIndicatorCalculator();
        var results = await calculator.CalculateAsync(
            Ticker(),
            Definition("VOLUME", "D", """{"period":5}"""),
            candles,
            CancellationToken.None);

        var latest = results[^1];
        Assert.Equal(200m / 120m, latest.PrimaryValue);
        Assert.Equal(120m, latest.SecondaryValue);
        Assert.Equal(2000m, latest.TertiaryValue);
    }

    [Fact]
    public async Task AtrCalculator_ReturnsPercentOfClose()
    {
        var candles = Enumerable.Range(0, 16)
            .Select(i => Candle(i, 100m + i, high: 102m + i, low: 98m + i))
            .ToList();
        var calculator = new AtrIndicatorCalculator();
        var results = await calculator.CalculateAsync(
            Ticker(),
            Definition("ATR", "D", """{"period":14}"""),
            candles,
            CancellationToken.None);

        Assert.NotEmpty(results);
        Assert.NotNull(results[^1].PrimaryValue);
        Assert.True(results[^1].PrimaryValue > 0);
        Assert.NotNull(results[^1].SecondaryValue);
    }

    [Fact]
    public async Task PriceRangeCalculator_MeasuresDistanceFromWindowHigh()
    {
        var candles = Enumerable.Range(0, 5)
            .Select(i => Candle(i, 90m + i, high: 90m + i, low: 80m))
            .ToList();
        candles[^1] = Candle(4, 90m, high: 90m, low: 80m);
        var calculator = new PriceRangeIndicatorCalculator();
        var results = await calculator.CalculateAsync(
            Ticker(),
            Definition("PRICE_RANGE", "D", """{"period":5}"""),
            candles,
            CancellationToken.None);

        var latest = results[^1];
        Assert.True(latest.PrimaryValue < 0);
        Assert.Equal(80m, ReadDecimal(latest.Values, "periodLow"));
    }

    [Fact]
    public async Task RocCalculator_MeasuresPercentChangeVsPriorClose()
    {
        var candles = new[]
        {
            Candle(0, 100m),
            Candle(1, 110m),
            Candle(2, 121m)
        };
        var calculator = new RocIndicatorCalculator();
        var results = await calculator.CalculateAsync(
            Ticker(),
            Definition("ROC", "D", """{"period":2}"""),
            candles,
            CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(21m, results[0].PrimaryValue);
        Assert.Equal(100m, ReadDecimal(results[0].Values, "priorClose"));
    }

    [Fact]
    public async Task RelativeStrengthCalculator_SubtractsBenchmarkReturn()
    {
        var start = new DateOnly(2020, 1, 2);
        var stock = new[]
        {
            Candle(0, 100m),
            Candle(1, 100m),
            Candle(2, 110m)
        };
        var spy = new (DateOnly Date, decimal Close)[]
        {
            (start, 50m),
            (start.AddDays(1), 50m),
            (start.AddDays(2), 50m)
        };
        var calculator = new RelativeStrengthIndicatorCalculator(new FixedBenchmarkLookup(spy));
        var results = await calculator.CalculateAsync(
            Ticker(),
            Definition("RELATIVE_STRENGTH", "D", """{"period":2,"benchmarkSymbol":"SPY"}"""),
            stock,
            CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(10m, results[0].PrimaryValue);
        Assert.Equal(10m, ReadDecimal(results[0].Values, "stockReturnPct"));
        Assert.Equal(0m, ReadDecimal(results[0].Values, "benchmarkReturnPct"));
    }

    private static Ticker Ticker() => new()
    {
        Symbol = "TEST",
        Exchange = "NASDAQ",
        Country = "US"
    };

    private static IndicatorDefinition Definition(string type, string timeframe, string parameters)
        => new()
        {
            Code = type,
            Name = type,
            IndicatorType = type,
            Timeframe = timeframe,
            ParametersJson = parameters
        };

    private static PriceCandle Candle(int offset, decimal close, decimal? high = null, decimal? low = null, long? volume = 100)
        => new()
        {
            TickerId = 1,
            Timeframe = "D",
            CandleDate = new DateOnly(2020, 1, 2).AddDays(offset),
            Open = close,
            High = high ?? close,
            Low = low ?? close,
            Close = close,
            AdjustedClose = close,
            Volume = volume,
            Source = "test"
        };

    private static decimal? ReadDecimal(IReadOnlyDictionary<string, object?> values, string key)
        => values.TryGetValue(key, out var value) && value is decimal number ? number : null;

    private sealed class FixedBenchmarkLookup(IReadOnlyList<(DateOnly Date, decimal Close)> closes)
        : TechnicalScanner.Application.Abstractions.IBenchmarkPriceLookup
    {
        public IReadOnlyList<(DateOnly Date, decimal Close)> GetCloses(string symbol, string timeframe)
            => closes;
    }
}
