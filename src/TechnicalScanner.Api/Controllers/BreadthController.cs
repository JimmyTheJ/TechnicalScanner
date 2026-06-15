using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechnicalScanner.Application.Abstractions;

namespace TechnicalScanner.Api.Controllers;

[ApiController]
[Route("api/breadth")]
public sealed class BreadthController(ITechnicalScannerDbContext dbContext) : ControllerBase
{
    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest(CancellationToken cancellationToken)
    {
        var latestScan = await dbContext.BreadthSnapshots
            .OrderByDescending(b => b.ScanDateUtc)
            .Select(b => b.ScanDateUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestScan == default)
        {
            return Ok(Array.Empty<object>());
        }

        var snapshots = await dbContext.BreadthSnapshots
            .Where(b => b.ScanDateUtc == latestScan)
            .OrderBy(b => b.MetricCode)
            .ToListAsync(cancellationToken);

        return Ok(snapshots);
    }
}
