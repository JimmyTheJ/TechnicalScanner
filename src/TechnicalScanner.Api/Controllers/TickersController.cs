using Microsoft.AspNetCore.Mvc;
using TechnicalScanner.Application.Models.Queries;
using TechnicalScanner.Application.Services.Queries;

namespace TechnicalScanner.Api.Controllers;

[ApiController]
[Route("api/tickers")]
public sealed class TickersController(
    TickerQueryService tickerQueryService,
    SignalQueryService signalQueryService) : ControllerBase
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

    [HttpGet("{symbol}/candles")]
    [ProducesResponseType(typeof(IReadOnlyList<PriceCandlePointDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GetCandles(
        string symbol,
        [FromQuery] string timeframe = "D",
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] int limit = 400)
    {
        try
        {
            return Ok(tickerQueryService.GetCandles(symbol, timeframe, from, to, limit));
        }
        catch (ArgumentOutOfRangeException)
        {
            return BadRequest(new { error = "timeframe must be D, W, or M." });
        }
    }

    [HttpGet("{symbol}/signals")]
    [ProducesResponseType(typeof(IReadOnlyList<TickerSignalHistoryDto>), StatusCodes.Status200OK)]
    public IActionResult GetSignals(string symbol, [FromQuery] int limit = 40)
        => Ok(signalQueryService.GetHistoryForTicker(symbol, limit));
}
