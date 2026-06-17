using Microsoft.AspNetCore.Mvc;
using TechnicalScanner.Application.Services.Queries;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Api.Controllers;

[ApiController]
[Route("api/alerts")]
public sealed class AlertsController(AlertQueryService alertQueryService) : ControllerBase
{
    [HttpGet("events")]
    [ProducesResponseType(typeof(IReadOnlyList<AlertEvent>), StatusCodes.Status200OK)]
    public IActionResult GetEvents([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        => Ok(alertQueryService.GetEvents(page, pageSize));

    [HttpGet("events/{id:long}")]
    [ProducesResponseType(typeof(AlertEvent), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetEvent(long id)
    {
        var alertEvent = alertQueryService.GetEvent(id);
        return alertEvent is null ? NotFound() : Ok(alertEvent);
    }

    [HttpGet("events/{id:long}/deliveries")]
    [ProducesResponseType(typeof(IReadOnlyList<AlertDelivery>), StatusCodes.Status200OK)]
    public IActionResult GetDeliveries(long id)
        => Ok(alertQueryService.GetDeliveries(id));
}
