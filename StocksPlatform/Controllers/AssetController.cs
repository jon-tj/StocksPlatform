using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;
using StocksPlatform.Models;

namespace StocksPlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AssetController(AppDbContext db, UserManager<AppUser> userManager) : ControllerBase
{
    public record AssetDto(Guid Id, string Name, string Type);
    public record HistoryDto(double[] Returns, string[] Times);

    // GET /api/asset — returns the user's followed asset IDs (defaults to Guid.Empty)
    [HttpGet]
    public async Task<ActionResult<AssetDto[]>> GetMyAssets()
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        var assetIds = await db.UserPortfolios
            .Where(p => p.UserId == user.Id)
            .Select(p => p.AssetId)
            .ToListAsync();

        if (assetIds.Count == 0)
            assetIds.Add(Guid.Empty);

        var assets = await db.Assets
            .Where(a => assetIds.Contains(a.Id))
            .ToListAsync();

        // If any IDs aren't found (e.g. Guid.Empty not seeded yet), include defaults
        return Ok(assets.Select(a => new AssetDto(a.Id, a.Name, a.Type.ToString())).ToArray());
    }

    // GET /api/asset/{id} — asset details
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssetDto>> GetAsset(Guid id)
    {
        var asset = await db.Assets.FindAsync(id);
        if (asset is null)
            return NotFound();

        return Ok(new AssetDto(asset.Id, asset.Name, asset.Type.ToString()));
    }

    // GET /api/asset/{id}/history?timeFrom=2025-01-01
    [HttpGet("{id:guid}/history")]
    public IActionResult GetHistory(Guid id, [FromQuery] DateTime? timeFrom)
    {
        var from = timeFrom?.Date ?? DateTime.UtcNow.AddYears(-1).Date;
        var to = DateTime.UtcNow.Date;

        if (from > to)
            return BadRequest("timeFrom must be before today.");

        var (returns, times) = GenerateMockDailyReturns(id, from, to);
        return Ok(new HistoryDto(returns, times));
    }

    private static (double[] Returns, string[] Times) GenerateMockDailyReturns(Guid assetId, DateTime from, DateTime to)
    {
        // Seed with asset ID so the same asset always produces the same shape
        var seed = Math.Abs(assetId.GetHashCode());
        var rng = new Random(seed);

        var returnsList = new List<double>();
        var timesList = new List<string>();

        for (var d = from; d <= to; d = d.AddDays(1))
        {
            // Skip weekends
            if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;

            // Random daily return: normally distributed around 0.04% with ±1.5% std dev
            var u1 = 1.0 - rng.NextDouble();
            var u2 = 1.0 - rng.NextDouble();
            var normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            var dailyReturn = Math.Round(0.04 + normal * 1.5, 2);

            returnsList.Add(dailyReturn);
            timesList.Add(d.ToString("MMM d"));
        }

        return (returnsList.ToArray(), timesList.ToArray());
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        return email is null ? null : await userManager.FindByEmailAsync(email);
    }
}
