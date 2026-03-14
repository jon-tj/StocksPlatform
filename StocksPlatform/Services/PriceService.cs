using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;
using StocksPlatform.Models;

namespace StocksPlatform.Services;

public class PriceService(AppDbContext db)
{
    /// <summary>
    /// Returns the current price for the given asset.
    /// Fetches (or refreshes) via the stub provider if the cached price is missing or expired.
    /// </summary>
    public async Task<decimal> GetPriceAsync(Guid assetId)
    {
        var now = DateTime.UtcNow;

        var cached = await db.AssetPrices
            .FirstOrDefaultAsync(p => p.AssetId == assetId);

        if (cached is not null && cached.ExpiresAt > now)
            return cached.Price;

        var price = await FetchPriceAsync(assetId);
        var expiry = now.AddMinutes(15);

        if (cached is null)
        {
            db.AssetPrices.Add(new AssetPrice
            {
                AssetId = assetId,
                Price = price,
                FetchedAt = now,
                ExpiresAt = expiry,
            });
        }
        else
        {
            cached.Price = price;
            cached.FetchedAt = now;
            cached.ExpiresAt = expiry;
        }

        await db.SaveChangesAsync();
        return price;
    }

    /// <summary>
    /// Returns prices for a collection of asset IDs, batching cache lookups.
    /// </summary>
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

        // Fetch stale/missing prices
        foreach (var id in staleIds)
        {
            var price = await FetchPriceAsync(id);
            var expiry = now.AddMinutes(15);

            if (cachedMap.TryGetValue(id, out var existing))
            {
                existing.Price = price;
                existing.FetchedAt = now;
                existing.ExpiresAt = expiry;
                result[id] = price;
            }
            else
            {
                var entry = new AssetPrice { AssetId = id, Price = price, FetchedAt = now, ExpiresAt = expiry };
                db.AssetPrices.Add(entry);
                result[id] = price;
            }
        }

        if (staleIds.Count > 0)
            await db.SaveChangesAsync();

        // Fill from cache for non-stale
        foreach (var id in ids.Except(staleIds))
            result[id] = cachedMap[id].Price;

        return result;
    }

    /// <summary>
    /// Stub price provider — returns a deterministic mock price based on asset ID.
    /// Replace with a real market data feed in production.
    /// </summary>
    private static Task<decimal> FetchPriceAsync(Guid assetId)
    {
        // Seed from asset ID for a stable, realistic-looking price
        var seed = Math.Abs(assetId.GetHashCode());
        var rng = new Random(seed);
        var price = Math.Round(50m + (decimal)(rng.NextDouble() * 450), 2);
        return Task.FromResult(price);
    }
}
