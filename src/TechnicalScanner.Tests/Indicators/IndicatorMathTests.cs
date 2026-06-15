using TechnicalScanner.Application.Indicators;

namespace TechnicalScanner.Tests.Indicators;

public class IndicatorMathTests
{
    [Fact]
    public void SimpleMovingAverage_ReturnsExpectedValue()
    {
        var values = new decimal[] { 10, 20, 30, 40, 50 };
        var sma = IndicatorMath.SimpleMovingAverage(values, 5);
        Assert.Equal(30m, sma);
    }

    [Fact]
    public void ExponentialMovingAverage_ReturnsValue()
    {
        var values = Enumerable.Range(1, 30).Select(i => (decimal)i).ToList();
        var ema = IndicatorMath.ExponentialMovingAverage(values, 10);
        Assert.NotNull(ema);
        Assert.True(ema > 20);
    }

    [Fact]
    public void RelativeStrengthIndex_ClassifiesOversoldOnDecliningSeries()
    {
        var values = Enumerable.Range(0, 30).Select(i => 100m - i).ToList();
        var rsi = IndicatorMath.RelativeStrengthIndex(values, 14);
        Assert.NotNull(rsi);
        Assert.True(rsi < 50);
    }

    [Fact]
    public void BollingerBands_ReturnBandsAroundMean()
    {
        var values = Enumerable.Repeat(100m, 25).ToList();
        var (middle, upper, lower, _) = IndicatorMath.BollingerBands(values, 20, 2m);
        Assert.Equal(100m, middle);
        Assert.Equal(100m, upper);
        Assert.Equal(100m, lower);
    }

    [Fact]
    public void ClassifyRsi_MapsThresholds()
    {
        Assert.Equal("Oversold", IndicatorMath.ClassifyRsi(25));
        Assert.Equal("Overbought", IndicatorMath.ClassifyRsi(72));
    }
}
