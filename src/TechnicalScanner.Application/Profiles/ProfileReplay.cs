using System.Text.Json;
using TechnicalScanner.Application.Models.Profiles;
using TechnicalScanner.Application.Signals;

namespace TechnicalScanner.Application.Profiles;

public static class ProfileReplay
{
    public static ProfileBacktestResultDto Replay(
        string profileCode,
        string symbol,
        string ruleJson,
        IReadOnlyList<ScoreWeightSpec> weights,
        IReadOnlyList<DateOnly> dailyDates,
        IReadOnlyDictionary<string, IReadOnlyList<(DateOnly Date, Dictionary<string, JsonElement> Fields)>> series,
        IReadOnlyList<(DateOnly Date, decimal Close)> closes,
        int forwardDays,
        int maxEntries = 400)
    {
        var evaluator = new SignalRuleEvaluator();
        var pointers = series.Keys.ToDictionary(code => code, _ => -1, StringComparer.OrdinalIgnoreCase);
        var closeByDate = closes.ToDictionary(c => c.Date, c => c.Close);
        var closeList = closes.OrderBy(c => c.Date).ToList();
        var matchDays = 0;
        var previousMatch = false;
        var previousSnapshot = (Dictionary<string, Dictionary<string, JsonElement>>?)null;
        var entries = new List<ProfileBacktestHitDto>();

        foreach (var date in dailyDates)
        {
            var snapshot = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (code, points) in series)
            {
                var pointer = pointers[code];
                while (pointer + 1 < points.Count && points[pointer + 1].Date <= date)
                {
                    pointer++;
                }

                pointers[code] = pointer;
                if (pointer >= 0)
                {
                    snapshot[code] = points[pointer].Fields;
                }
            }

            var prior = previousSnapshot;
            var isMatch = evaluator.Evaluate(ruleJson, snapshot, prior);
            previousSnapshot = snapshot;
            if (!isMatch)
            {
                previousMatch = false;
                continue;
            }

            matchDays++;
            if (previousMatch)
            {
                continue;
            }

            previousMatch = true;
            if (!closeByDate.TryGetValue(date, out var close))
            {
                continue;
            }

            var (score, _) = ProfileScoring.Score(weights, snapshot, evaluator, prior);
            entries.Add(new ProfileBacktestHitDto
            {
                Date = date,
                Close = close,
                ForwardReturnPct = ForwardReturn(closeList, date, close, forwardDays),
                Score = weights.Count == 0 ? null : score
            });
        }

        var recent = entries.Count <= maxEntries
            ? entries
            : entries.Skip(entries.Count - maxEntries).ToList();

        return new ProfileBacktestResultDto
        {
            ProfileCode = profileCode,
            Symbol = symbol,
            MatchDays = matchDays,
            EntryCount = entries.Count,
            ForwardDays = forwardDays,
            Entries = recent
        };
    }

    private static decimal? ForwardReturn(
        IReadOnlyList<(DateOnly Date, decimal Close)> closes,
        DateOnly entryDate,
        decimal entryClose,
        int forwardDays)
    {
        if (forwardDays <= 0 || entryClose == 0)
        {
            return null;
        }

        var target = entryDate.AddDays(forwardDays);
        foreach (var (date, close) in closes)
        {
            if (date >= target)
            {
                return IndicatorMathForward(entryClose, close);
            }
        }

        return null;
    }

    private static decimal IndicatorMathForward(decimal entryClose, decimal laterClose)
        => (laterClose - entryClose) / entryClose * 100m;
}
