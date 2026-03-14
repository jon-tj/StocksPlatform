using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;
using StocksPlatform.Models;

namespace StocksPlatform.Services;

public class FractionService(AppDbContext db, AssetPriceService priceService)
{
    /// <summary>
    /// Returns the PortfolioAsset rows for <paramref name="portfolioId"/> with up-to-date
    /// fractions. Any rows whose FractionExpiry has passed (or was never set) are recomputed
    /// from current prices and persisted before returning.
    /// </summary>
    public async Task<List<PortfolioAsset>> GetFreshChildrenAsync(Guid portfolioId)
    {
        var now = DateTime.UtcNow;

        var rows = await db.PortfolioAssets
            .Where(pa => pa.PortfolioId == portfolioId)
            .Include(pa => pa.Asset)
            .ToListAsync();

        if (rows.Count == 0)
            return rows;

        var stale = rows.Where(pa => pa.FractionExpiry is null || pa.FractionExpiry <= now).ToList();
        if (stale.Count > 0)
        {
            var allPrices = await priceService.GetPricesAsync(rows.Select(pa => pa.AssetId));
            var totalValue = rows.Sum(pa => (double)allPrices[pa.AssetId] * pa.Quantity);
            var expiry = now.Date.AddDays(1);

            foreach (var pa in stale)
            {
                var assetValue = (double)allPrices[pa.AssetId] * pa.Quantity;
                pa.Fraction = totalValue > 0 ? assetValue / totalValue : 0;
                pa.FractionExpiry = expiry;
            }

            await db.SaveChangesAsync();
        }

        return rows;
    }
}
