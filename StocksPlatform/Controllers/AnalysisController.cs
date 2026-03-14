using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;
using StocksPlatform.Models;

namespace StocksPlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnalysisController(AppDbContext db) : ControllerBase
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
        double InstitutionalOrderFlowDelta
    );

    // GET /api/analysis/{assetId}
    // Returns up to 365 daily delta snapshots for the given asset,
    // computing and storing any missing days on demand (stub: all values = 1).
    [HttpGet("{assetId:guid}")]
    public async Task<ActionResult<DeltaDto[]>> GetDeltas(Guid assetId)
    {
        var asset = await db.Assets.FindAsync(assetId);
        if (asset is null) return NotFound();

        var cutoff = DateTime.UtcNow.Date.AddDays(-364);

        // Remove entries older than one year
        var stale = await db.AssetDeltas
            .Where(d => d.AssetId == assetId && d.Date < cutoff)
            .ToListAsync();
        if (stale.Count > 0)
        {
            db.AssetDeltas.RemoveRange(stale);
            await db.SaveChangesAsync();
        }

        // Find which dates in the past year are missing
        var existing = await db.AssetDeltas
            .Where(d => d.AssetId == assetId && d.Date >= cutoff)
            .ToListAsync();

        var existingDates = existing.Select(d => d.Date.Date).ToHashSet();

        var missing = Enumerable
            .Range(0, 365)
            .Select(i => cutoff.AddDays(i).Date)
            .Where(d => d <= DateTime.UtcNow.Date && !existingDates.Contains(d))
            .ToList();

        if (missing.Count > 0)
        {
            var computed = missing.Select(date => Compute(assetId, date));
            db.AssetDeltas.AddRange(computed);
            await db.SaveChangesAsync();
            existing.AddRange(computed);
        }

        var result = existing
            .OrderBy(d => d.Date)
            .Select(d => ToDto(d, asset.Name))
            .ToArray();

        return Ok(result);
    }

    // GET /api/analysis/{assetId}/latest
    // Returns the single most-recent delta snapshot.
    [HttpGet("{assetId:guid}/latest")]
    public async Task<ActionResult<DeltaDto>> GetLatest(Guid assetId)
    {
        var asset = await db.Assets.FindAsync(assetId);
        if (asset is null) return NotFound();

        var today = DateTime.UtcNow.Date;

        var row = await db.AssetDeltas
            .Where(d => d.AssetId == assetId && d.Date == today)
            .FirstOrDefaultAsync();

        if (row is null)
        {
            row = Compute(assetId, today);
            db.AssetDeltas.Add(row);
            await db.SaveChangesAsync();
        }

        return Ok(ToDto(row, asset.Name));
    }

    private static DeltaDto ToDto(AssetDelta d, string assetName) => new(
        d.AssetId, assetName, d.Date,
        d.MarketDelta, d.PairDelta, d.PairAssetId,
        d.PublicSentimentDelta, d.MemberSentimentDelta,
        d.FundamentalDelta, d.InstitutionalOrderFlowDelta);

    /// <summary>
    /// Stub computation — all deltas return 1.0 until real algorithms are wired in.
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
    };
}
