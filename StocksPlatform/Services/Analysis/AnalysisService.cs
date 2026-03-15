using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;
using StocksPlatform.Models;

namespace StocksPlatform.Services.Analysis;

/// <summary>
/// Orchestrates delta caching, computation and refresh for all assets.
/// Leaf delta signals are delegated to <see cref="IAssetDeltaProvider"/> implementations;
/// portfolio deltas are computed as a fraction-weighted average of their children.
/// </summary>
public class AnalysisService(
    AppDbContext db,
    FractionService fractionService,
    FundInstitutionalService fundInstitutionalService,
    PatternDeltaService patternDeltaService)
{
    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns stored daily delta snapshots for the asset (up to 365 days),
    /// refreshing any expired rows in-place. Never backfills missing dates.
    /// </summary>
    public async Task<List<AssetDelta>> GetHistoryAsync(Guid assetId)
    {
        await fundInstitutionalService.EnsureTodaySnapshotsAsync();

        var now = DateTime.UtcNow;
        var cutoff = now.Date.AddDays(-364);

        var tooOld = await db.AssetDeltas
            .Where(d => d.AssetId == assetId && d.Date < cutoff)
            .ToListAsync();
        if (tooOld.Count > 0)
        {
            db.AssetDeltas.RemoveRange(tooOld);
            await db.SaveChangesAsync();
        }

        var existing = await db.AssetDeltas
            .Where(d => d.AssetId == assetId && d.Date >= cutoff)
            .ToListAsync();

        var expiredRows = existing.Where(d => d.ExpiresAt is null || d.ExpiresAt <= now).ToList();
        if (expiredRows.Count > 0)
        {
            var cache = new Dictionary<(Guid, DateTime), AssetDelta>(
                existing.Where(d => d.ExpiresAt > now)
                        .Select(d => KeyValuePair.Create((d.AssetId, d.Date.Date), d)));
            var childrenCache = new Dictionary<Guid, List<Guid>>();

            foreach (var row in expiredRows)
                await RefreshDeltaAsync(row, cache, childrenCache);

            await db.SaveChangesAsync();
        }

        return [.. existing.OrderBy(d => d.Date)];
    }

    /// <summary>
    /// Returns today's delta snapshot, computing or refreshing it if needed.
    /// Pass <paramref name="skipCache"/> = <c>true</c> to force a recompute even if the cached value has not expired.
    /// </summary>
    public async Task<AssetDelta> GetLatestAsync(Guid assetId, bool skipCache = false)
    {
        await fundInstitutionalService.EnsureTodaySnapshotsAsync();

        var now = DateTime.UtcNow;
        var today = now.Date;
        var cache = new Dictionary<(Guid, DateTime), AssetDelta>();
        var childrenCache = new Dictionary<Guid, List<Guid>>();

        var row = await db.AssetDeltas
            .Where(d => d.AssetId == assetId && d.Date == today)
            .FirstOrDefaultAsync();

        if (row is null)
            row = await EnsureDeltaAsync(assetId, today, cache, childrenCache);
        else if (skipCache || row.ExpiresAt is null || row.ExpiresAt <= now)
            await RefreshDeltaAsync(row, cache, childrenCache);

        await db.SaveChangesAsync();
        return row;
    }

    /// <summary>
    /// Returns the stored delta for a specific date, or <c>null</c> if none exists.
    /// Does not compute missing snapshots for historical dates.
    /// </summary>
    public async Task<AssetDelta?> GetAtAsync(Guid assetId, DateTime date) =>
        await db.AssetDeltas
            .Where(d => d.AssetId == assetId && d.Date == date.Date)
            .FirstOrDefaultAsync();

    /// <summary>
    /// Returns today's delta for every child of a portfolio asset.
    /// </summary>
    public async Task<List<(string Name, AssetDelta Delta)>> GetHoldingDeltasAsync(Guid assetId)
    {
        await fundInstitutionalService.EnsureTodaySnapshotsAsync();

        var children = await fractionService.GetFreshChildrenAsync(assetId);
        if (children.Count == 0) return [];

        var now = DateTime.UtcNow;
        var today = now.Date;
        var cache = new Dictionary<(Guid, DateTime), AssetDelta>();
        var childrenCache = new Dictionary<Guid, List<Guid>>();

        var results = new List<(string Name, AssetDelta Delta)>(children.Count);
        foreach (var pa in children)
        {
            var row = await db.AssetDeltas
                .Where(d => d.AssetId == pa.AssetId && d.Date == today)
                .FirstOrDefaultAsync();

            if (row is null)
                row = await EnsureDeltaAsync(pa.AssetId, today, cache, childrenCache);
            else if (row.ExpiresAt is null || row.ExpiresAt <= now)
                await RefreshDeltaAsync(row, cache, childrenCache);
            else
                cache[(pa.AssetId, today)] = row;

            results.Add((pa.Asset.Name, row));
        }

        await db.SaveChangesAsync();
        return results;
    }

    // -------------------------------------------------------------------------
    // Scoring helpers (public so the controller can use them when mapping DTOs)
    // -------------------------------------------------------------------------

    public static double Score(AssetDelta d) =>
        (d.MarketDelta + d.PublicSentimentDelta + d.MemberSentimentDelta +
         d.FundamentalDelta + d.InstitutionalOrderFlowDelta + d.PatternDelta) / 6.0;

    public static double[] Softmax(double[] scores)
    {
        var max = scores.Max();
        var exps = scores.Select(s => Math.Exp(s - max)).ToArray();
        var sum = exps.Sum();
        return exps.Select(e => e / sum).ToArray();
    }

    // -------------------------------------------------------------------------
    // Private orchestration
    // -------------------------------------------------------------------------

    private async Task<AssetDelta> EnsureDeltaAsync(
        Guid assetId, DateTime date,
        Dictionary<(Guid, DateTime), AssetDelta> cache,
        Dictionary<Guid, List<Guid>> childrenCache)
    {
        var key = (assetId, date.Date);
        if (cache.TryGetValue(key, out var cached))
            return cached;

        var now = DateTime.UtcNow;

        var existing = await db.AssetDeltas
            .Where(d => d.AssetId == assetId && d.Date == date.Date)
            .FirstOrDefaultAsync();

        if (existing is not null)
        {
            if (existing.ExpiresAt > now)
            {
                cache[key] = existing;
                return existing;
            }
            await RefreshDeltaAsync(existing, cache, childrenCache);
            return existing;
        }

        var delta = await ComputeDeltaAsync(assetId, date.Date, cache, childrenCache);
        db.AssetDeltas.Add(delta);
        cache[key] = delta;
        return delta;
    }

    private async Task RefreshDeltaAsync(
        AssetDelta row,
        Dictionary<(Guid, DateTime), AssetDelta> cache,
        Dictionary<Guid, List<Guid>> childrenCache)
    {
        var fresh = await ComputeDeltaAsync(row.AssetId, row.Date, cache, childrenCache);
        row.MarketDelta = fresh.MarketDelta;
        row.PairDelta = fresh.PairDelta;
        row.PairAssetId = fresh.PairAssetId;
        row.PublicSentimentDelta = fresh.PublicSentimentDelta;
        row.MemberSentimentDelta = fresh.MemberSentimentDelta;
        row.FundamentalDelta = fresh.FundamentalDelta;
        row.InstitutionalOrderFlowDelta = fresh.InstitutionalOrderFlowDelta;
        row.PatternDelta = fresh.PatternDelta;
        row.ExpiresAt = fresh.ExpiresAt;
        cache[(row.AssetId, row.Date.Date)] = row;
    }

    /// <summary>
    /// Builds a new (unsaved) AssetDelta. For portfolio assets this is a
    /// fraction-weighted average of their children; for leaf assets it calls
    /// the individual delta providers.
    /// </summary>
    private async Task<AssetDelta> ComputeDeltaAsync(
        Guid assetId, DateTime date,
        Dictionary<(Guid, DateTime), AssetDelta> cache,
        Dictionary<Guid, List<Guid>> childrenCache)
    {
        if (!childrenCache.TryGetValue(assetId, out var childIds))
        {
            childIds = await db.PortfolioAssets
                .Where(pa => pa.PortfolioId == assetId)
                .Select(pa => pa.AssetId)
                .ToListAsync();
            childrenCache[assetId] = childIds;
        }

        if (childIds.Count > 0)
        {
            var children = await fractionService.GetFreshChildrenAsync(assetId);
            var fractionMap = children.ToDictionary(pa => pa.AssetId, pa => pa.Fraction ?? (1.0 / children.Count));

            var childDeltas = new List<(AssetDelta Delta, double Weight)>(childIds.Count);
            foreach (var childId in childIds)
            {
                var childDelta = await EnsureDeltaAsync(childId, date, cache, childrenCache);
                var weight = fractionMap.TryGetValue(childId, out var f) ? f : 0.0;
                childDeltas.Add((childDelta, weight));
            }

            return WeightedAverageOf(assetId, date, childDeltas);
        }

        return await ComputeLeafAsync(assetId, date);
    }

    private static AssetDelta WeightedAverageOf(
        Guid assetId, DateTime date,
        List<(AssetDelta Delta, double Weight)> children)
    {
        var totalWeight = children.Sum(c => c.Weight);
        if (totalWeight <= 0)
        {
            var eq = 1.0 / children.Count;
            children = children.Select(c => (c.Delta, eq)).ToList();
            totalWeight = 1.0;
        }

        double Wavg(Func<AssetDelta, double> selector) =>
            children.Sum(c => selector(c.Delta) * c.Weight) / totalWeight;

        return new AssetDelta
        {
            AssetId = assetId,
            Date = date,
            MarketDelta = Wavg(d => d.MarketDelta),
            PairDelta = null,
            PairAssetId = null,
            PublicSentimentDelta = Wavg(d => d.PublicSentimentDelta),
            MemberSentimentDelta = Wavg(d => d.MemberSentimentDelta),
            FundamentalDelta = Wavg(d => d.FundamentalDelta),
            InstitutionalOrderFlowDelta = Wavg(d => d.InstitutionalOrderFlowDelta),
            PatternDelta = Wavg(d => d.PatternDelta),
            ExpiresAt = DateTime.UtcNow.Date.AddDays(1),
        };
    }

    /// <summary>
    /// Leaf computation for assets with no children.
    /// Delegates to registered <see cref="IAssetDeltaProvider"/> implementations.
    /// Stub fields (1.0) will be replaced as their providers are implemented.
    /// </summary>
    private async Task<AssetDelta> ComputeLeafAsync(Guid assetId, DateTime date)
    {
        var institutionalDelta = await fundInstitutionalService.GetInstitutionalDeltaAsync(assetId);
        var patternDelta = await patternDeltaService.ComputeAsync(assetId, date);

        return new AssetDelta
        {
            AssetId = assetId,
            Date = date,
            MarketDelta = 1.0,
            PairDelta = null,
            PairAssetId = null,
            PublicSentimentDelta = 1.0,
            MemberSentimentDelta = 1.0,
            FundamentalDelta = 1.0,
            InstitutionalOrderFlowDelta = institutionalDelta,
            PatternDelta = patternDelta,
            ExpiresAt = DateTime.UtcNow.Date.AddDays(1),
        };
    }
}
