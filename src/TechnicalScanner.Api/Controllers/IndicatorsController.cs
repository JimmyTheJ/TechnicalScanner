using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TechnicalScanner.Application.Models.Queries;
using TechnicalScanner.Application.Services.Queries;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Api.Controllers;

[ApiController]
[Route("api/indicators")]
public sealed class IndicatorsController(IndicatorQueryService indicatorQueryService) : ControllerBase
{
    [HttpGet("definitions")]
    [ProducesResponseType(typeof(IReadOnlyList<IndicatorDefinition>), StatusCodes.Status200OK)]
    public IActionResult GetDefinitions()
        => Ok(indicatorQueryService.GetDefinitions());

    [HttpPost("definitions")]
    [ProducesResponseType(typeof(IndicatorDefinition), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateDefinition(
        [FromBody] CreateIndicatorDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var definition = indicatorQueryService.CreateDefinition(request);
            await indicatorQueryService.SaveDefinitionAsync(cancellationToken);
            return CreatedAtAction(nameof(GetDefinitions), new { code = definition.Code }, definition);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "ParametersJson must be valid JSON." });
        }
    }

    [HttpGet("latest")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GetLatest([FromQuery] string ticker)
    {
        if (string.IsNullOrWhiteSpace(ticker))
        {
            return BadRequest(new { error = "ticker is required." });
        }

        return Ok(indicatorQueryService.GetLatestForTicker(ticker));
    }

    [HttpGet("history")]
    [ProducesResponseType(typeof(IReadOnlyList<IndicatorHistoryPointDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GetHistory(
        [FromQuery] string ticker,
        [FromQuery] string indicatorCode,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to)
    {
        if (string.IsNullOrWhiteSpace(ticker) || string.IsNullOrWhiteSpace(indicatorCode))
        {
            return BadRequest(new { error = "ticker and indicatorCode are required." });
        }

        return Ok(indicatorQueryService.GetHistory(ticker, indicatorCode, from, to));
    }
}
