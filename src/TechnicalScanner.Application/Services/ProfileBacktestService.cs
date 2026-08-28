using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Models.Profiles;
using TechnicalScanner.Application.Profiles;
using TechnicalScanner.Application.Services.Queries;

namespace TechnicalScanner.Application.Services;

public sealed class ProfileBacktestService(
    ITechnicalScannerDbContext dbContext,
    ScanProfileQueryService scanProfileQueryService)
{
    public ProfileBacktestResultDto Replay(
        string profileCode,
        string symbol,
        int forwardDays,
        DateOnly? from,
        DateOnly? to)
    {
        var profile = scanProfileQueryService.Find(profileCode)
            ?? throw new InvalidOperationException($"Profile '{profileCode}' was not found.");

        var ticker = dbContext.Tickers.FirstOrDefault(t => t.Symbol == symbol && t.IsActive)
            ?? throw new InvalidOperationException($"Ticker '{symbol}' was not found.");

        var specs = ProfileJson.Deserialize<List<ProfileIndicatorSpec>>(profile.IndicatorsJson);
        var codes = specs.Select(s => s.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var definitions = dbContext.IndicatorDefinitions
            .Where(d => codes.Contains(d.Code))
            .ToList()
            .ToDictionary(d => d.Id, d => d.Code);

        var series = new Dictionary<string, IReadOnlyList<(DateOnly Date, Dictionary<string, System.Text.Json.JsonElement> Fields)>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var code in codes)
        {
            series[code] = [];
        }

            var definitionIds = definitions.Keys.ToList();
        var values = dbContext.IndicatorValues
            .Where(v => v.TickerId == ticker.Id && definitionIds.Contains(v.IndicatorDefinitionId))
            .OrderBy(v => v.CandleDate)
            .ToList();

        foreach (var group in values.GroupBy(v => definitions[v.IndicatorDefinitionId]))
        {
            series[group.Key] = group
                .Select(v => (v.CandleDate, IndicatorValueMaps.ParseValuesJson(v.ValuesJson)))
                .ToList();
        }

        var daily = dbContext.PriceCandles
            .Where(p => p.TickerId == ticker.Id && p.Timeframe == "D")
            .OrderBy(p => p.CandleDate)
            .ToList();

        if (from is not null)
        {
            daily = daily.Where(p => p.CandleDate >= from.Value).ToList();
        }

        if (to is not null)
        {
            daily = daily.Where(p => p.CandleDate <= to.Value).ToList();
        }

        var weights = ProfileJson.Deserialize<List<ScoreWeightSpec>>(profile.ScoreWeightsJson);
        var dates = daily.Select(p => p.CandleDate).ToList();
        var closes = daily.Select(p => (p.CandleDate, p.AdjustedClose ?? p.Close)).ToList();

        return ProfileReplay.Replay(
            profile.Code,
            ticker.Symbol,
            profile.RuleJson,
            weights,
            dates,
            series,
            closes,
            Math.Clamp(forwardDays, 1, 252));
    }
}
