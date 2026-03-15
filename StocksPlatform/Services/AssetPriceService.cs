using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;
using StocksPlatform.Models;
using StocksPlatform.Services.PriceServices;

namespace StocksPlatform.Services;

/// <summary>
/// Cache layer for asset prices.
/// Tries Yahoo Finance first for any asset whose market maps to a Yahoo suffix;
/// falls back to E24 for Oslo/NASDAQ stocks, then to a deterministic stub.
/// </summary>
public class AssetPriceService(AppDbContext db, YahooPriceService yahoo, E24PriceService e24)
{
    /// <summary>Returns the current price for the given asset, using a 1-minute cache.</summary>
    public async Task<decimal> GetPriceAsync(Guid assetId)
    {
        var now = DateTime.UtcNow;
        var cached = await db.AssetPrices.FirstOrDefaultAsync(p => p.AssetId == assetId);

        if (cached is not null && cached.ExpiresAt > now)
            return cached.Price;

        var price = await FetchLivePriceAsync(assetId);
        UpsertCachedPrice(cached, assetId, price, now);
        await db.SaveChangesAsync();
        return price;
    }

    /// <summary>Returns prices for multiple assets, batching cache lookups.</summary>
    public async Task<Dictionary<Guid, decimal>> GetPricesAsync(IEnumerable<Guid> assetIds)
    {
        var ids = assetIds.Distinct().ToList();
        var now = DateTime.UtcNow;
        var result = new Dictionary<Guid, decimal>();

        var cached = await db.AssetPrices
            .Where(p => ids.Contains(p.AssetId))
            .ToListAsync();

        var cachedMap = cached.ToDictionary(p => p.AssetId);
        var staleIds = ids.Where(id => !cachedMap.TryGetValue(id, out var c) || c.ExpiresAt <= now).ToList();

        foreach (var id in staleIds)
        {
            var price = await FetchLivePriceAsync(id);
            cachedMap.TryGetValue(id, out var existing);
            UpsertCachedPrice(existing, id, price, now);
            result[id] = price;
        }

        if (staleIds.Count > 0)
            await db.SaveChangesAsync();

        foreach (var id in ids.Except(staleIds))
            result[id] = cachedMap[id].Price;

        return result;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<decimal> FetchLivePriceAsync(Guid assetId)
    {
        var asset = await db.Assets.FindAsync(assetId);

        // Try Yahoo if the asset's exchange is in the Yahoo suffix map
        if (asset?.Symbol is { } symbol && YahooPriceService.BuildTicker(symbol, asset.Market) is not null)
        {
            var yahooPrice = await yahoo.TryFetchLivePriceAsync(assetId);
            if (yahooPrice.HasValue) return yahooPrice.Value;
        }

        // Fall back to E24 (handles XOSL / XNAS with real data; stubs everything else)
        return await e24.FetchLivePriceAsync(assetId);
    }

    private void UpsertCachedPrice(AssetPrice? existing, Guid assetId, decimal price, DateTime now)
    {
        var expiry = now.AddMinutes(1);
        if (existing is null)
            db.AssetPrices.Add(new AssetPrice { AssetId = assetId, Price = price, FetchedAt = now, ExpiresAt = expiry });
        else
        {
            existing.Price = price;
            existing.FetchedAt = now;
            existing.ExpiresAt = expiry;
        }
    }
}
