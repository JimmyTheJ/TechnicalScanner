using Microsoft.AspNetCore.Mvc;
using TechnicalScanner.Application.Models.Queries;
using TechnicalScanner.Application.Services.Queries;

namespace TechnicalScanner.Api.Controllers;

[ApiController]
[Route("api/scanner")]
public sealed class ScannerController(
    ScannerQueryService scannerQueryService,
    ScreenerQueryService screenerQueryService,
    SignalQueryService signalQueryService) : ControllerBase
{
    [HttpGet("latest")]
    [ProducesResponseType(typeof(ScannerLatestDto), StatusCodes.Status200OK)]
    public IActionResult GetLatest()
        => Ok(scannerQueryService.GetLatest());

    [HttpGet("screener")]
    [ProducesResponseType(typeof(PagedResult<ScreenerRowDto>), StatusCodes.Status200OK)]
    public IActionResult GetScreener([FromQuery] ScreenerQuery query, CancellationToken cancellationToken)
        => Ok(screenerQueryService.GetScreener(query, cancellationToken));

    [HttpGet("signals")]
    [ProducesResponseType(typeof(PagedResult<SignalMatchDto>), StatusCodes.Status200OK)]
    public IActionResult GetSignals(
        [FromQuery] string? signal,
        [FromQuery] string? country,
        [FromQuery] string? exchange,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = QueryPaging.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => Ok(signalQueryService.GetMatches(signal, country, exchange, page, pageSize, cancellationToken));

    [HttpGet("scores")]
    [ProducesResponseType(typeof(PagedResult<ScoreRowDto>), StatusCodes.Status200OK)]
    public IActionResult GetScores(
        [FromQuery] string? scoreCode,
        [FromQuery] string? country,
        [FromQuery] string? exchange,
        [FromQuery] decimal? minScore,
        [FromQuery] decimal? maxScore,
        [FromQuery] string sort = "compositeScoreDesc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = QueryPaging.DefaultPageSize)
        => Ok(scannerQueryService.GetScores(scoreCode, country, exchange, minScore, maxScore, sort, page, pageSize));
}
