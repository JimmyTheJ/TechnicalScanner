using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechnicalScanner.Application.Abstractions;

namespace TechnicalScanner.Api.Controllers;

[ApiController]
[Route("api/tickers")]
public sealed class TickersController(ITechnicalScannerDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetTickers(CancellationToken cancellationToken)
    {
        var tickers = await dbContext.Tickers
            .OrderBy(t => t.Symbol)
            .Select(t => new
            {
                t.Id,
                t.Symbol,
                t.Exchange,
                t.Country,
                t.CompanyName,
                t.Sector,
                t.Industry,
                t.MarketCap,
                t.IsEtf
            })
            .ToListAsync(cancellationToken);

        return Ok(tickers);
    }

    [HttpGet("{symbol}")]
    public async Task<IActionResult> GetTicker(string symbol, CancellationToken cancellationToken)
    {
        var ticker = await dbContext.Tickers
            .Where(t => t.Symbol == symbol)
            .Select(t => new
            {
                t.Id,
                t.Symbol,
                t.Exchange,
                t.Country,
                t.CompanyName,
                t.Sector,
                t.Industry,
                t.MarketCap,
                t.IsEtf
            })
            .FirstOrDefaultAsync(cancellationToken);

        return ticker is null ? NotFound() : Ok(ticker);
    }
}
