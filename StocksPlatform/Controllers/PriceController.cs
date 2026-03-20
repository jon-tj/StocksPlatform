using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;
using StocksPlatform.Services;

namespace StocksPlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PriceController(AppDbContext db, AssetPriceService priceService) : ControllerBase
{
    public record PriceDto(Guid AssetId, string Symbol, decimal Price, DateTime FetchedAt, DateTime ExpiresAt);
    public record LivePriceDto(
        Guid AssetId,
        string Symbol,
        decimal Price,
        double? DayGainPercent,
        DateTime FetchedAt,
        DateTime ExpiresAt);

    // GET /api/price/{assetId}
    [HttpGet("{assetId:guid}")]
    public async Task<ActionResult<PriceDto>> GetPrice(Guid assetId)
    {
        var asset = await db.Assets.FindAsync(assetId);
        if (asset is null) return NotFound();

        var price = await priceService.GetPriceAsync(assetId);

        var cached = await db.AssetPrices
            .FirstOrDefaultAsync(p => p.AssetId == assetId);

        return Ok(new PriceDto(
            assetId,
            asset.Symbol ?? asset.Name,
            price,
            cached!.FetchedAt,
            cached!.ExpiresAt));
    }

    // GET /api/price?assetIds=id1&assetIds=id2...
    [HttpGet]
    public async Task<ActionResult<PriceDto[]>> GetPrices([FromQuery] Guid[] assetIds)
    {
        if (assetIds.Length == 0) return Ok(Array.Empty<PriceDto>());

        var assets = await db.Assets
            .Where(a => assetIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        var prices = await priceService.GetPricesAsync(assetIds);

        var cached = await db.AssetPrices
            .Where(p => assetIds.Contains(p.AssetId))
            .ToDictionaryAsync(p => p.AssetId);

        var result = assetIds
            .Where(id => assets.ContainsKey(id))
            .Select(id => new PriceDto(
                id,
                assets[id].Symbol ?? assets[id].Name,
                prices[id],
                cached[id].FetchedAt,
                cached[id].ExpiresAt))
            .ToArray();

        return Ok(result);
    }

    // GET /api/price/live/{assetId}
    [HttpGet("live/{assetId:guid}")]
    public async Task<ActionResult<LivePriceDto>> GetLivePrice(Guid assetId)
    {
        var asset = await db.Assets.FindAsync(assetId);
        if (asset is null) return NotFound();

        var price = await priceService.GetPriceAsync(assetId);
        var cached = await db.AssetPrices.FirstOrDefaultAsync(p => p.AssetId == assetId);
        var dayRef = await GetDayReferencePriceAsync(assetId);
        double? dayGain = dayRef is > 0
            ? (double)((price - dayRef.Value) / dayRef.Value * 100m)
            : null;

        return Ok(new LivePriceDto(
            assetId,
            asset.Symbol ?? asset.Name,
            price,
            dayGain,
            cached!.FetchedAt,
            cached!.ExpiresAt));
    }

    // GET /api/price/live?assetIds=id1&assetIds=id2...
    [HttpGet("live")]
    public async Task<ActionResult<LivePriceDto[]>> GetLivePrices([FromQuery] Guid[] assetIds)
    {
        if (assetIds.Length == 0) return Ok(Array.Empty<LivePriceDto>());

        var ids = assetIds.Distinct().ToArray();
        var assets = await db.Assets
            .Where(a => ids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        var prices = await priceService.GetPricesAsync(ids);
        var cached = await db.AssetPrices
            .Where(p => ids.Contains(p.AssetId))
            .ToDictionaryAsync(p => p.AssetId);

        var result = new List<LivePriceDto>(ids.Length);
        foreach (var id in ids)
        {
            if (!assets.ContainsKey(id) || !prices.ContainsKey(id) || !cached.ContainsKey(id)) continue;

            var dayRef = await GetDayReferencePriceAsync(id);
            double? dayGain = dayRef is > 0
                ? (double)((prices[id] - dayRef.Value) / dayRef.Value * 100m)
                : null;

            result.Add(new LivePriceDto(
                id,
                assets[id].Symbol ?? assets[id].Name,
                prices[id],
                dayGain,
                cached[id].FetchedAt,
                cached[id].ExpiresAt));
        }

        return Ok(result.ToArray());
    }

    private async Task<decimal?> GetDayReferencePriceAsync(Guid assetId)
    {
        var latestDaily = await db.AssetDailyHistory
            .Where(h => h.AssetId == assetId)
            .OrderByDescending(h => h.Timestamp)
            .Select(h => (decimal?)h.Price)
            .FirstOrDefaultAsync();

        return latestDaily;
    }
}
