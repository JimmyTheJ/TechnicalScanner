namespace TechnicalScanner.Application.Profiles;

public static class ProfileBacktestStats
{
    public static decimal? Median(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var sorted = values.OrderBy(value => value).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2m
            : sorted[mid];
    }

    public static decimal? Mean(IReadOnlyList<decimal> values)
        => values.Count == 0 ? null : values.Average();
}
