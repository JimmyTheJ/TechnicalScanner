using Microsoft.AspNetCore.Mvc;
using TechnicalScanner.Application.Models.Queries;
using TechnicalScanner.Application.Services.Queries;

namespace TechnicalScanner.Api.Controllers;

[ApiController]
[Route("api/tickers")]
public sealed class TickersController(TickerQueryService tickerQueryService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TickerListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTickers([FromQuery] TickerListQuery query, CancellationToken cancellationToken)
    {
        var result = await tickerQueryService.GetTickersAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{symbol}")]
    [ProducesResponseType(typeof(TickerListItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetTicker(string symbol)
    {
        var ticker = tickerQueryService.GetTickerBySymbol(symbol);
        return ticker is null ? NotFound() : Ok(ticker);
    }
}
