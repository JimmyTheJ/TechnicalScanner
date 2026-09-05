namespace TechnicalScanner.Application.Services;

internal static class IncrementalPriceFreshness
{
    public static bool HasCurrentSession(DateOnly? latestCandleDate, DateOnly utcToday)
        => latestCandleDate is not null
           && latestCandleDate.Value >= LastCompletedSessionDate(utcToday);

    public static DateOnly LastCompletedSessionDate(DateOnly utcToday)
        => utcToday.DayOfWeek switch
        {
            DayOfWeek.Sunday => utcToday.AddDays(-2),
            DayOfWeek.Saturday => utcToday.AddDays(-1),
            _ => utcToday
        };
}
