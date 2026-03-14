using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;
using StocksPlatform.Models;
using StocksPlatform.Services;

namespace StocksPlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AssetController(AppDbContext db, UserManager<AppUser> userManager, AssetPriceService assetPriceService) : ControllerBase
{
    public record AssetDto(Guid Id, string Name, string Type, string? Symbol, string? Market, string? Broker, string? BrokerSymbol);
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
        return Ok(assets.Select(a => new AssetDto(a.Id, a.Name, a.Type.ToString(), a.Symbol, a.Market, a.Broker, a.BrokerSymbol)).ToArray());
    }

    // GET /api/asset/{id} — asset details
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssetDto>> GetAsset(Guid id)
    {
        var asset = await db.Assets.FindAsync(id);
        if (asset is null)
            return NotFound();

        return Ok(new AssetDto(asset.Id, asset.Name, asset.Type.ToString(), asset.Symbol, asset.Market, asset.Broker, asset.BrokerSymbol));
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

        var exchangeSuffix = asset.Market?.ToUpperInvariant() switch
        {
            "OSE" => "OSE",
            "NASDAQ" => "NAS",
            _ => null
        };

        if (exchangeSuffix is not null && asset.Symbol is { } symbol)
        {
            if (intraday)
            {
                await assetPriceService.EnsureIntradayBarsAsync(id, symbol, exchangeSuffix);
                var bars = await db.AssetIntradayHistory
                    .Where(b => b.AssetId == id)
                    .OrderBy(b => b.Timestamp)
                    .ToListAsync();
                return Ok(PriceToHistory(bars, intraday: true));
            }
            else
            {
                await assetPriceService.EnsureDailyBarsAsync(id, symbol, exchangeSuffix);
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
