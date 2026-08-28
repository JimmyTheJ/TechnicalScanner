using System.Text.Json;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Models.Queries;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Services.Queries;

public sealed class IndicatorQueryService(ITechnicalScannerDbContext dbContext)
{
    public IReadOnlyList<IndicatorDefinition> GetDefinitions()
        => dbContext.IndicatorDefinitions
            .Where(d => d.IsActive)
            .OrderBy(d => d.Code)
            .ToList();

    public IndicatorDefinition CreateDefinition(CreateIndicatorDefinitionRequest request)
    {
        var existing = dbContext.IndicatorDefinitions.FirstOrDefault(d => d.Code == request.Code);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Indicator definition '{request.Code}' already exists.");
        }

        _ = JsonDocument.Parse(request.ParametersJson);

        var definition = new IndicatorDefinition
        {
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            IndicatorType = request.IndicatorType,
            Timeframe = request.Timeframe,
            ParametersJson = request.ParametersJson,
            NeedsRecalculation = true
        };

        dbContext.Add(definition);
        return definition;
    }

    public async Task SaveDefinitionAsync(CancellationToken cancellationToken)
        => await dbContext.SaveChangesAsync(cancellationToken);

    public IReadOnlyList<object> GetLatestForTicker(string symbol)
    {
        return dbContext.IndicatorValues
            .Join(dbContext.Tickers, v => v.TickerId, t => t.Id, (v, t) => new { v, t })
            .Join(dbContext.IndicatorDefinitions, x => x.v.IndicatorDefinitionId, d => d.Id, (x, d) => new { x.v, x.t, d })
            .Where(x => x.t.Symbol == symbol)
            .AsEnumerable()
            .GroupBy(x => x.d.Code)
            .Select(g => g.OrderByDescending(x => x.v.CandleDate).First())
            .OrderBy(x => x.d.Code)
            .Select(x => new
            {
                x.d.Code,
                x.d.Name,
                x.v.CandleDate,
                x.v.PrimaryValue,
                x.v.SecondaryValue,
                x.v.TertiaryValue,
                Values = JsonSerializer.Deserialize<JsonElement>(x.v.ValuesJson)
            })
            .Cast<object>()
            .ToList();
    }

    public IReadOnlyList<IndicatorHistoryPointDto> GetHistory(
        string symbol,
        string indicatorCode,
        DateOnly? from,
        DateOnly? to)
    {
        var query = dbContext.IndicatorValues
            .Join(dbContext.Tickers, v => v.TickerId, t => t.Id, (v, t) => new { v, t })
            .Join(dbContext.IndicatorDefinitions, x => x.v.IndicatorDefinitionId, d => d.Id, (x, d) => new { x.v, x.t, d })
            .Where(x => x.t.Symbol == symbol && x.d.Code == indicatorCode);

        if (from is not null)
        {
            query = query.Where(x => x.v.CandleDate >= from);
        }

        if (to is not null)
        {
            query = query.Where(x => x.v.CandleDate <= to);
        }

        return query
            .OrderBy(x => x.v.CandleDate)
            .Select(x => new IndicatorHistoryPointDto
            {
                CandleDate = x.v.CandleDate,
                PrimaryValue = x.v.PrimaryValue,
                SecondaryValue = x.v.SecondaryValue,
                TertiaryValue = x.v.TertiaryValue,
                ValuesJson = x.v.ValuesJson
            })
            .ToList();
    }
}
