using TechnicalScanner.Application.Services;
using TechnicalScanner.Domain.Enums;

namespace TechnicalScanner.Tests.Services;

public class CandleAggregationServiceTests
{
    [Fact]
    public void TimeframeExtensions_ParseWeeklyAndMonthlyCodes()
    {
        Assert.Equal(Timeframe.W, TimeframeExtensions.FromCode("W"));
        Assert.Equal(Timeframe.M, TimeframeExtensions.FromCode("m"));
        Assert.Equal("D", Timeframe.D.ToCode());
    }
}
