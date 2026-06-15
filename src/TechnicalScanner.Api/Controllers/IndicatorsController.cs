using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechnicalScanner.Application.Abstractions;

namespace TechnicalScanner.Api.Controllers;

[ApiController]
[Route("api/indicators")]
public sealed class IndicatorsController(ITechnicalScannerDbContext dbContext) : ControllerBase
{
    [HttpGet("definitions")]
    public async Task<IActionResult> GetDefinitions(CancellationToken cancellationToken)
    {
        var definitions = await dbContext.IndicatorDefinitions
            .Where(d => d.IsActive)
            .OrderBy(d => d.Code)
            .ToListAsync(cancellationToken);

        return Ok(definitions);
    }

    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest([FromQuery] string ticker, CancellationToken cancellationToken)
    {
        var values = await dbContext.IndicatorValues
            .Join(dbContext.Tickers, v => v.TickerId, t => t.Id, (v, t) => new { v, t })
            .Join(dbContext.IndicatorDefinitions, x => x.v.IndicatorDefinitionId, d => d.Id, (x, d) => new { x.v, x.t, d })
            .Where(x => x.t.Symbol == ticker)
            .GroupBy(x => x.d.Code)
            .Select(g => g.OrderByDescending(x => x.v.CandleDate).First())
            .Select(x => new
            {
                x.d.Code,
                x.v.CandleDate,
                x.v.PrimaryValue,
                x.v.SecondaryValue,
                x.v.TertiaryValue,
                x.v.ValuesJson
            })
            .ToListAsync(cancellationToken);

        return Ok(values);
    }
}
