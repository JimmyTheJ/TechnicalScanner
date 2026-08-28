using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Models.Profiles;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Profiles;

public static class ProfileUniverseQuery
{
    public static IQueryable<Ticker> ApplyTickerFilters(IQueryable<Ticker> query, ProfileUniverse universe)
    {
        if (!string.IsNullOrWhiteSpace(universe.Country))
        {
            query = query.Where(t => t.Country == universe.Country);
        }

        if (!universe.IncludeEtfs)
        {
            query = query.Where(t => !t.IsEtf);
        }

        return query;
    }

    public static HashSet<long> ApplyMarketFilters(
        ITechnicalScannerDbContext dbContext,
        HashSet<long> tickerIds,
        ProfileUniverse universe)
    {
        if (tickerIds.Count == 0)
        {
            return tickerIds;
        }

        if (universe.MinHistoryDays is > 0)
        {
            var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-universe.MinHistoryDays.Value));
            var oldEnough = dbContext.PriceCandles
                .Where(p => p.Timeframe == "D" && tickerIds.Contains(p.TickerId))
                .GroupBy(p => p.TickerId)
                .Select(g => new { TickerId = g.Key, FirstDate = g.Min(p => p.CandleDate) })
                .Where(x => x.FirstDate <= cutoff)
                .Select(x => x.TickerId)
                .ToHashSet();
            tickerIds.IntersectWith(oldEnough);
        }

        if (universe.MinDollarVolume is > 0 && tickerIds.Count > 0)
        {
            var latestDates = dbContext.PriceCandles
                .Where(p => p.Timeframe == "D" && tickerIds.Contains(p.TickerId))
                .GroupBy(p => p.TickerId)
                .Select(g => new { TickerId = g.Key, Date = g.Max(p => p.CandleDate) });

            var liquid = (
                from p in dbContext.PriceCandles
                join d in latestDates on new { p.TickerId, p.CandleDate } equals new { d.TickerId, CandleDate = d.Date }
                where p.Timeframe == "D"
                select new
                {
                    p.TickerId,
                    DollarVolume = (p.AdjustedClose ?? p.Close) * (p.Volume ?? 0)
                })
                .AsEnumerable()
                .Where(x => x.DollarVolume >= universe.MinDollarVolume)
                .Select(x => x.TickerId)
                .ToHashSet();

            tickerIds.IntersectWith(liquid);
        }

        return tickerIds;
    }
}
