using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TechnicalScanner.Application.Models.Profiles;
using TechnicalScanner.Application.Services;
using TechnicalScanner.Application.Services.Queries;

namespace TechnicalScanner.Api.Controllers;

[ApiController]
[Route("api/profiles")]
public sealed class ProfilesController(
    ScanProfileQueryService scanProfileQueryService,
    ProfileBacktestService profileBacktestService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ScanProfileDto>), StatusCodes.Status200OK)]
    public IActionResult GetAll() => Ok(scanProfileQueryService.GetAll());

    [HttpGet("{code}")]
    [ProducesResponseType(typeof(ScanProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetByCode(string code)
    {
        var profile = scanProfileQueryService.GetByCode(code);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ScanProfileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] UpsertScanProfileRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var profile = scanProfileQueryService.Create(request);
            await scanProfileQueryService.SaveAsync(cancellationToken);
            return CreatedAtAction(nameof(GetByCode), new { code = profile.Code }, profile);
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

    [HttpPut("{code}")]
    [ProducesResponseType(typeof(ScanProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        string code,
        [FromBody] UpsertScanProfileRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var profile = scanProfileQueryService.Update(code, request);
            await scanProfileQueryService.SaveAsync(cancellationToken);
            return Ok(profile);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "RuleJson must be valid JSON." });
        }
    }

    [HttpDelete("{code}")]
    [ProducesResponseType(typeof(ScanProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(string code, CancellationToken cancellationToken)
    {
        try
        {
            var profile = scanProfileQueryService.Deactivate(code);
            await scanProfileQueryService.SaveAsync(cancellationToken);
            return Ok(profile);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("{code}/backtest")]
    [ProducesResponseType(typeof(ProfileBacktestResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Backtest(
        string code,
        [FromQuery] string ticker,
        [FromQuery] int forwardDays = 20,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null)
    {
        if (string.IsNullOrWhiteSpace(ticker))
        {
            return BadRequest(new { error = "ticker is required." });
        }

        try
        {
            return Ok(profileBacktestService.Replay(code, ticker, forwardDays, from, to));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("{code}/universe-backtest")]
    [ProducesResponseType(typeof(ProfileUniverseBacktestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetUniverseBacktest(string code)
    {
        var result = profileBacktestService.GetLatestUniverse(code);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{code}/universe-backtest")]
    [ProducesResponseType(typeof(ProfileUniverseBacktestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RunUniverseBacktest(
        string code,
        [FromQuery] int forwardDays = 20,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await profileBacktestService.RunUniverseAsync(code, forwardDays, from, to, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
