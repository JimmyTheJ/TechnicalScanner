using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TechnicalScanner.Application.Models.Queries;
using TechnicalScanner.Application.Services.Queries;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Api.Controllers;

[ApiController]
[Route("api/signals")]
public sealed class SignalsController(SignalQueryService signalQueryService) : ControllerBase
{
    [HttpGet("definitions")]
    [ProducesResponseType(typeof(IReadOnlyList<SignalDefinition>), StatusCodes.Status200OK)]
    public IActionResult GetDefinitions()
        => Ok(signalQueryService.GetDefinitions());

    [HttpPost("definitions")]
    [ProducesResponseType(typeof(SignalDefinition), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateDefinition(
        [FromBody] CreateSignalDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var definition = signalQueryService.CreateDefinition(request);
            await signalQueryService.SaveDefinitionAsync(cancellationToken);
            return CreatedAtAction(nameof(GetDefinitions), new { code = definition.Code }, definition);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "RuleJson must be valid JSON." });
        }
    }
}
