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
}
