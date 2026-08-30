using System.Text.Json;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Models.Queries;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Services.Queries;

public sealed class SignalQueryService(ITechnicalScannerDbContext dbContext)
{
    public IReadOnlyList<SignalDefinition> GetDefinitions()
        => dbContext.SignalDefinitions
            .Where(s => s.IsActive)
            .OrderBy(s => s.Code)
            .ToList();

    public SignalDefinition CreateDefinition(CreateSignalDefinitionRequest request)
    {
        var existing = dbContext.SignalDefinitions.FirstOrDefault(s => s.Code == request.Code);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Signal definition '{request.Code}' already exists.");
        }

        if (string.IsNullOrWhiteSpace(request.RuleJson))
        {
            throw new InvalidOperationException("RuleJson is required.");
        }

        _ = JsonDocument.Parse(request.RuleJson);

        var definition = new SignalDefinition
        {
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            RuleJson = request.RuleJson,
            Severity = request.Severity,
            Direction = request.Direction
        };

        dbContext.Add(definition);
        return definition;
    }

    public async Task SaveDefinitionAsync(CancellationToken cancellationToken)
        => await dbContext.SaveChangesAsync(cancellationToken);

    public PagedResult<SignalMatchDto> GetMatches(
        string? signalCode,
        string? country,
        string? exchange,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize, skip) = QueryPaging.Normalize(page, pageSize);
        var latestScanDate = dbContext.SignalResults
            .OrderByDescending(r => r.ScanDateUtc)
            .Select(r => (DateTimeOffset?)r.ScanDateUtc)
            .FirstOrDefault();

        if (latestScanDate is null)
        {
            return new PagedResult<SignalMatchDto>([], normalizedPage, normalizedPageSize, 0);
        }

        var query = dbContext.SignalResults
            .Where(r => r.ScanDateUtc == latestScanDate && r.IsMatch)
            .Join(dbContext.Tickers, r => r.TickerId, t => t.Id, (r, t) => new { r, t })
            .Join(dbContext.SignalDefinitions, x => x.r.SignalDefinitionId, s => s.Id, (x, s) => new { x.r, x.t, s })
            .Where(x => x.t.IsActive);

        if (!string.IsNullOrWhiteSpace(signalCode))
        {
            query = query.Where(x => x.s.Code == signalCode);
        }

        if (!string.IsNullOrWhiteSpace(country))
        {
            query = query.Where(x => x.t.Country == country);
        }

        if (!string.IsNullOrWhiteSpace(exchange))
        {
            query = query.Where(x => x.t.Exchange == exchange);
        }

        var totalCount = query.Count();
        var items = query
            .OrderBy(x => x.t.Symbol)
            .Skip(skip)
            .Take(normalizedPageSize)
            .Select(x => new SignalMatchDto
            {
                TickerId = x.t.Id,
                Symbol = x.t.Symbol,
                Exchange = x.t.Exchange,
                Country = x.t.Country,
                SignalCode = x.s.Code,
                SignalName = x.s.Name,
                Score = x.r.Score
            })
            .ToList();

        return new PagedResult<SignalMatchDto>(items, normalizedPage, normalizedPageSize, totalCount);
    }

    public IReadOnlyList<TickerSignalHistoryDto> GetHistoryForTicker(string symbol, int limit)
    {
        var tickerId = dbContext.Tickers
            .Where(t => t.Symbol == symbol && t.IsActive)
            .Select(t => t.Id)
            .FirstOrDefault();
        if (tickerId == 0)
        {
            return [];
        }

        var latestScanDate = dbContext.SignalResults
            .Where(r => r.TickerId == tickerId)
            .Max(r => (DateTimeOffset?)r.ScanDateUtc);
        if (latestScanDate is null)
        {
            return [];
        }

        var take = Math.Clamp(limit <= 0 ? 40 : limit, 1, 200);
        return dbContext.SignalResults
            .Join(dbContext.SignalDefinitions, r => r.SignalDefinitionId, s => s.Id, (r, s) => new { r, s })
            .Where(x => x.r.TickerId == tickerId && x.r.ScanDateUtc == latestScanDate.Value)
            .OrderByDescending(x => x.r.IsMatch)
            .ThenBy(x => x.s.Code)
            .Take(take)
            .Select(x => new TickerSignalHistoryDto
            {
                ScanDateUtc = x.r.ScanDateUtc,
                SignalCode = x.s.Code,
                SignalName = x.s.Name,
                IsMatch = x.r.IsMatch
            })
            .ToList();
    }
}
