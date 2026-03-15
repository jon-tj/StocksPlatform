using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;
using StocksPlatform.Models;
using StocksPlatform.Services;
using StocksPlatform.Services.PriceServices;

namespace StocksPlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AssetController(AppDbContext db, UserManager<AppUser> userManager, E24PriceService e24, YahooPriceService yahoo) : ControllerBase
{
    public record AssetDto(Guid Id, string Name, string Type, string? Symbol, string? Market, string? Broker, string? BrokerSymbol, string? Country, string? Region, string? Sector, string? Subsector, string? IconUrl, string? WebsiteUrl, string? Description, string? Ceo, string? Address1, string? Address2, long? NumberShares);
    public record HistoryDto(double[] Prices, string[] Times);

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
        return Ok(assets.Select(a => new AssetDto(a.Id, a.Name, a.Type.ToString(), a.Symbol, a.Market, a.Broker, a.BrokerSymbol, a.Country, a.Region, a.Sector, a.Subsector, a.IconUrl, a.WebsiteUrl, a.Description, a.Ceo, a.Address1, a.Address2, a.NumberShares)).ToArray());
    }

    // GET /api/asset/search?q=query — search all assets by name or symbol
    [HttpGet("search")]
    public async Task<ActionResult<AssetDto[]>> SearchAssets([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(Array.Empty<AssetDto>());

        var lower = q.ToLower();
        var results = await db.Assets
            .Where(a => a.Name.ToLower().Contains(lower) || (a.Symbol != null && a.Symbol.ToLower().Contains(lower)))
            .Take(50)
            .Select(a => new { a.Id, a.Name, a.Type, a.Symbol, a.Market, a.Broker, a.BrokerSymbol, a.Popularity, a.Country, a.Region, a.Sector, a.Subsector, a.IconUrl })
            .ToListAsync();

        var exact = q.ToUpperInvariant();
        var ordered = results
            .OrderByDescending(a => a.Symbol?.ToUpperInvariant() == exact || a.Name.ToUpperInvariant() == exact)
            .ThenByDescending(a => (a.Market?.ToUpperInvariant() == "XOSL" ? 10000 : 0) + (a.Popularity ?? 0))
            .Take(10)
            .Select(a => new AssetDto(a.Id, a.Name, a.Type.ToString(), a.Symbol, a.Market, a.Broker, a.BrokerSymbol, a.Country, a.Region, a.Sector, a.Subsector, a.IconUrl, null, null, null, null, null, null));

        return Ok(ordered);
    }

    // GET /api/asset/{id} — asset details
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssetDto>> GetAsset(Guid id)
    {
        var asset = await db.Assets.FindAsync(id);
        if (asset is null)
            return NotFound();

        return Ok(new AssetDto(asset.Id, asset.Name, asset.Type.ToString(), asset.Symbol, asset.Market, asset.Broker, asset.BrokerSymbol, asset.Country, asset.Region, asset.Sector, asset.Subsector, asset.IconUrl, asset.WebsiteUrl, asset.Description, asset.Ceo, asset.Address1, asset.Address2, asset.NumberShares));
    }

    // GET /api/asset/{id}/history?timeFrom=2025-01-01&intraday=false
    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<HistoryDto>> GetHistory(Guid id, [FromQuery] DateTime? timeFrom, [FromQuery] bool intraday = false)
    {
        var from = timeFrom?.Date ?? DateTime.UtcNow.AddYears(-1).Date;
        var to = DateTime.UtcNow.Date;

        if (from > to)
            return BadRequest("timeFrom must be before today.");

        var asset = await db.Assets.FindAsync(id);
        if (asset is null) return NotFound();

        // Yahoo first: covers all mapped exchanges including XOSL/XNAS
        if (asset.Symbol is { } yahooSymbol && YahooPriceService.BuildTicker(yahooSymbol, asset.Market) is not null)
        {
            if (intraday)
            {
                await yahoo.EnsureIntradayBarsAsync(id, yahooSymbol, asset.Market);
                var bars = await db.AssetIntradayHistory
                    .Where(b => b.AssetId == id)
                    .OrderBy(b => b.Timestamp)
                    .ToListAsync();
                return Ok(PriceToHistory(bars, intraday: true));
            }
            else
            {
                await yahoo.EnsureDailyBarsAsync(id, yahooSymbol, asset.Market);
                var bars = await db.AssetDailyHistory
                    .Where(b => b.AssetId == id && b.Timestamp >= from && b.Timestamp <= to)
                    .OrderBy(b => b.Timestamp)
                    .ToListAsync();
                return Ok(PriceToHistory(bars, intraday: false));
            }
        }

        // E24 fallback: XOSL / XNAS assets with no Yahoo mapping (shouldn't normally happen)
        var exchangeSuffix = asset.Market?.ToUpperInvariant() switch
        {
            "XOSL" => "OSE",
            "XNAS" => "NAS",
            _ => null
        };

        if (exchangeSuffix is not null && asset.Symbol is { } e24Symbol)
        {
            if (intraday)
            {
                await e24.EnsureIntradayBarsAsync(id, e24Symbol, exchangeSuffix);
                var bars = await db.AssetIntradayHistory
                    .Where(b => b.AssetId == id)
                    .OrderBy(b => b.Timestamp)
                    .ToListAsync();
                return Ok(PriceToHistory(bars, intraday: true));
            }
            else
            {
                await e24.EnsureDailyBarsAsync(id, e24Symbol, exchangeSuffix);
                var bars = await db.AssetDailyHistory
                    .Where(b => b.AssetId == id && b.Timestamp >= from && b.Timestamp <= to)
                    .OrderBy(b => b.Timestamp)
                    .ToListAsync();
                return Ok(PriceToHistory(bars, intraday: false));
            }
        }

        // Fallback for unsupported markets: return whatever history exists in DB
        var dailyBars = await db.AssetDailyHistory
            .Where(b => b.AssetId == id && b.Timestamp >= from && b.Timestamp <= to)
            .OrderBy(b => b.Timestamp)
            .ToListAsync();
        return Ok(PriceToHistory(dailyBars, intraday: false));
    }

    private static HistoryDto PriceToHistory(List<AssetDailyHistory> bars, bool intraday)
    {
        if (bars.Count < 2) return new HistoryDto([], []);
        string timeFormat = intraday ? "MMM d HH:mm" : "MMM d";
        return new HistoryDto(
            bars.Select(b => (double)b.Price).ToArray(),
            bars.Select(b => b.Timestamp.ToString(timeFormat)).ToArray()
        );
    }
    private static HistoryDto PriceToHistory(List<AssetIntradayHistory> bars, bool intraday)
    {
        if (bars.Count < 2) return new HistoryDto([], []);
        string timeFormat = intraday ? "MMM d HH:mm" : "MMM d";
        return new HistoryDto(
            bars.Select(b => (double)b.Price).ToArray(),
            bars.Select(b => b.Timestamp.ToString(timeFormat)).ToArray()
            );
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        return email is null ? null : await userManager.FindByEmailAsync(email);
    }
}
