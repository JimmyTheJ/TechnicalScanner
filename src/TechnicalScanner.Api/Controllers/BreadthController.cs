using Microsoft.AspNetCore.Mvc;
using TechnicalScanner.Application.Services.Queries;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Api.Controllers;

[ApiController]
[Route("api/breadth")]
public sealed class BreadthController(BreadthQueryService breadthQueryService) : ControllerBase
{
    [HttpGet("latest")]
    [ProducesResponseType(typeof(IReadOnlyList<BreadthSnapshot>), StatusCodes.Status200OK)]
    public IActionResult GetLatest()
        => Ok(breadthQueryService.GetLatest());

    [HttpGet("history")]
    [ProducesResponseType(typeof(IReadOnlyList<BreadthSnapshot>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GetHistory(
        [FromQuery] string metricCode,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to)
    {
        if (string.IsNullOrWhiteSpace(metricCode))
        {
            return BadRequest(new { error = "metricCode is required." });
        }

        return Ok(breadthQueryService.GetHistory(metricCode, from, to));
    }
}
