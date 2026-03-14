using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;
using StocksPlatform.Models;
using StocksPlatform.Services;

namespace StocksPlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnalysisController(AppDbContext db, FractionService fractionService) : ControllerBase
{
    public record DeltaDto(
        Guid AssetId,
        string AssetName,
        DateTime Date,
        double MarketDelta,
        double? PairDelta,
        Guid? PairAssetId,
        double PublicSentimentDelta,
        double MemberSentimentDelta,
        double FundamentalDelta,
        double InstitutionalOrderFlowDelta,
        double CombinedScore
    );

    public record HoldingDto(
        Guid AssetId,
        string AssetName,
        DateTime Date,
        double MarketDelta,
        double? PairDelta,
        Guid? PairAssetId,
        double PublicSentimentDelta,
        double MemberSentimentDelta,
        double FundamentalDelta,
        double InstitutionalOrderFlowDelta,
        double CombinedScore,
        double TargetFraction
    );

    // GET /api/analysis/{assetId}
    // Returns up to 365 daily delta snapshots for the given asset.
    // Missing dates are computed and inserted; expired rows are recomputed in-place.
    [HttpGet("{assetId:guid}")]
    public async Task<ActionResult<DeltaDto[]>> GetDeltas(Guid assetId)
    {
        var asset = await db.Assets.FindAsync(assetId);
        if (asset is null) return NotFound();

        var now = DateTime.UtcNow;
        var cutoff = now.Date.AddDays(-364);

        // Remove entries older than one year
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

        var existingDates = existing.Select(d => d.Date.Date).ToHashSet();
        var missingDates = Enumerable
            .Range(0, 365)
            .Select(i => cutoff.AddDays(i).Date)
            .Where(d => d <= now.Date && !existingDates.Contains(d))
            .ToList();

        var expiredRows = existing.Where(d => d.ExpiresAt is null || d.ExpiresAt <= now).ToList();

        if (missingDates.Count > 0 || expiredRows.Count > 0)
        {
            // Seed cache with already-fresh rows so children don't get re-queried
            var cache = new Dictionary<(Guid, DateTime), AssetDelta>(
                existing.Where(d => d.ExpiresAt > now)
                        .Select(d => KeyValuePair.Create((d.AssetId, d.Date.Date), d)));
            var childrenCache = new Dictionary<Guid, List<Guid>>();

            foreach (var row in expiredRows)
                await RefreshDeltaAsync(row, cache, childrenCache);

            foreach (var date in missingDates)
            {
                var delta = await EnsureDeltaAsync(assetId, date, cache, childrenCache);
                existing.Add(delta);
            }

            await db.SaveChangesAsync();
        }

        return Ok(existing.OrderBy(d => d.Date).Select(d => ToDto(d, asset.Name)).ToArray());
    }

    // GET /api/analysis/{assetId}/latest
    // Returns today's delta snapshot, recomputing if missing or expired.
    [HttpGet("{assetId:guid}/latest")]
    public async Task<ActionResult<DeltaDto>> GetLatest(Guid assetId)
    {
        var asset = await db.Assets.FindAsync(assetId);
        if (asset is null) return NotFound();

        var now = DateTime.UtcNow;
        var today = now.Date;

        var row = await db.AssetDeltas
            .Where(d => d.AssetId == assetId && d.Date == today)
            .FirstOrDefaultAsync();

        var cache = new Dictionary<(Guid, DateTime), AssetDelta>();
        var childrenCache = new Dictionary<Guid, List<Guid>>();

        if (row is null)
            row = await EnsureDeltaAsync(assetId, today, cache, childrenCache);
        else if (row.ExpiresAt is null || row.ExpiresAt <= now)
            await RefreshDeltaAsync(row, cache, childrenCache);

        await db.SaveChangesAsync();
        return Ok(ToDto(row, asset.Name));
    }

    // GET /api/analysis/{assetId}/at?date=2025-06-15
    // Returns the stored delta snapshot for a specific historical date.
    // Returns 204 No Content if no snapshot exists for that date (does not compute one).
    [HttpGet("{assetId:guid}/at")]
    public async Task<ActionResult<DeltaDto>> GetAt(Guid assetId, [FromQuery] DateTime date)
    {
        var asset = await db.Assets.FindAsync(assetId);
        if (asset is null) return NotFound();

        var row = await db.AssetDeltas
            .Where(d => d.AssetId == assetId && d.Date == date.Date)
            .FirstOrDefaultAsync();

        if (row is null) return NoContent();

        return Ok(ToDto(row, asset.Name));
    }

    // GET /api/analysis/{assetId}/holdings
    // Returns today's delta for every child of a portfolio asset,
    // enriched with a combined score and softmax-derived target fraction.
    [HttpGet("{assetId:guid}/holdings")]
    public async Task<ActionResult<HoldingDto[]>> GetHoldings(Guid assetId)
    {
        var asset = await db.Assets.FindAsync(assetId);
        if (asset is null) return NotFound();
        if (asset.Type != AssetType.Portfolio)
            return BadRequest("Asset is not a portfolio.");

        var children = await fractionService.GetFreshChildrenAsync(assetId);
        if (children.Count == 0) return Ok(Array.Empty<HoldingDto>());

        var now = DateTime.UtcNow;
        var today = now.Date;
        var cache = new Dictionary<(Guid, DateTime), AssetDelta>();
        var childrenCache = new Dictionary<Guid, List<Guid>>();

        var rows = new List<(string Name, AssetDelta Delta)>(children.Count);
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

            rows.Add((pa.Asset.Name, row));
        }

        await db.SaveChangesAsync();

        // Softmax over combined scores for target fractions
        var scores = rows.Select(r => Score(r.Delta)).ToArray();
        var fractions = Softmax(scores);

        return Ok(rows.Select((r, i) => ToHoldingDto(r.Delta, r.Name, scores[i], fractions[i])).ToArray());
    }
    /// it if not already present and fresh in the cache or DB.
    /// </summary>
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
            // Expired — refresh in-place
            await RefreshDeltaAsync(existing, cache, childrenCache);
            return existing;
        }

        var delta = await ComputeDeltaAsync(assetId, date.Date, cache, childrenCache);
        db.AssetDeltas.Add(delta);
        cache[key] = delta;
        return delta;
    }

    /// <summary>
    /// Recomputes all delta values on an existing tracked row and updates its expiry.
    /// </summary>
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
        row.ExpiresAt = fresh.ExpiresAt;
        cache[(row.AssetId, row.Date.Date)] = row;
    }

    /// <summary>
    /// Builds a new (unsaved) AssetDelta, using weighted-average of children if the asset
    /// is a portfolio, or the stub leaf computation otherwise.
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

        return Compute(assetId, date);
    }

    private static AssetDelta WeightedAverageOf(Guid assetId, DateTime date, List<(AssetDelta Delta, double Weight)> children)
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
            ExpiresAt = DateTime.UtcNow.Date.AddDays(1),
        };
    }

    private static DeltaDto ToDto(AssetDelta d, string assetName) => new(
        d.AssetId, assetName, d.Date,
        d.MarketDelta, d.PairDelta, d.PairAssetId,
        d.PublicSentimentDelta, d.MemberSentimentDelta,
        d.FundamentalDelta, d.InstitutionalOrderFlowDelta,
        Score(d));

    private static HoldingDto ToHoldingDto(AssetDelta d, string assetName, double score, double targetFraction) => new(
        d.AssetId, assetName, d.Date,
        d.MarketDelta, d.PairDelta, d.PairAssetId,
        d.PublicSentimentDelta, d.MemberSentimentDelta,
        d.FundamentalDelta, d.InstitutionalOrderFlowDelta,
        score, targetFraction);

    private static double Score(AssetDelta d) =>
        (d.MarketDelta + d.PublicSentimentDelta + d.MemberSentimentDelta +
         d.FundamentalDelta + d.InstitutionalOrderFlowDelta) / 5.0;

    private static double[] Softmax(double[] scores)
    {
        var max = scores.Max();
        var exps = scores.Select(s => Math.Exp(s - max)).ToArray();
        var sum = exps.Sum();
        return exps.Select(e => e / sum).ToArray();
    }

    /// <summary>
    /// Stub leaf computation — all deltas return 1.0 until real algorithms are wired in.
    /// Only called for assets with no children. Expires at midnight tomorrow UTC.
    /// </summary>
    private static AssetDelta Compute(Guid assetId, DateTime date) => new()
    {
        AssetId = assetId,
        Date = date,
        MarketDelta = 1.0,
        PairDelta = null,
        PairAssetId = null,
        PublicSentimentDelta = 1.0,
        MemberSentimentDelta = 1.0,
        FundamentalDelta = 1.0,
        InstitutionalOrderFlowDelta = 1.0,
        ExpiresAt = DateTime.UtcNow.Date.AddDays(1),
    };
}
