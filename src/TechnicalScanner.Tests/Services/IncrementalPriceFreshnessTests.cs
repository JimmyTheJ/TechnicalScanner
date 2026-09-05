using TechnicalScanner.Application.Services;

namespace TechnicalScanner.Tests.Services;

public class IncrementalPriceFreshnessTests
{
    [Fact]
    public void LastCompletedSessionDate_UsesFridayOnWeekends()
    {
        Assert.Equal(new DateOnly(2026, 9, 4), IncrementalPriceFreshness.LastCompletedSessionDate(new DateOnly(2026, 9, 5)));
        Assert.Equal(new DateOnly(2026, 9, 4), IncrementalPriceFreshness.LastCompletedSessionDate(new DateOnly(2026, 9, 6)));
        Assert.Equal(new DateOnly(2026, 9, 4), IncrementalPriceFreshness.LastCompletedSessionDate(new DateOnly(2026, 9, 4)));
        Assert.Equal(new DateOnly(2026, 9, 8), IncrementalPriceFreshness.LastCompletedSessionDate(new DateOnly(2026, 9, 8)));
    }

    [Fact]
    public void HasCurrentSession_SkipsTickersThatAlreadyHaveTheLatestSession()
    {
        Assert.True(IncrementalPriceFreshness.HasCurrentSession(new DateOnly(2026, 9, 4), new DateOnly(2026, 9, 5)));
        Assert.False(IncrementalPriceFreshness.HasCurrentSession(new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 5)));
        Assert.False(IncrementalPriceFreshness.HasCurrentSession(null, new DateOnly(2026, 9, 5)));
        Assert.True(IncrementalPriceFreshness.HasCurrentSession(new DateOnly(2026, 9, 8), new DateOnly(2026, 9, 8)));
        Assert.False(IncrementalPriceFreshness.HasCurrentSession(new DateOnly(2026, 9, 4), new DateOnly(2026, 9, 8)));
    }
}
