using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StocksPlatform.Data;
using StocksPlatform.Models;
using StocksPlatform.Services.Analysis;

namespace StocksPlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnalysisController(AppDbContext db, AnalysisService analysisService) : ControllerBase
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
        double PatternDelta,
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
        double PatternDelta,
        double CombinedScore,
        double TargetFraction
    );

    // GET /api/analysis/{assetId}
    // Returns stored daily delta snapshots for the given asset (up to 365 days).
    // Only refreshes rows that already exist but have expired — never backfills missing dates.
    // Today's snapshot is the only one ever auto-created (see /latest).
    [HttpGet("{assetId:guid}")]
    public async Task<ActionResult<DeltaDto[]>> GetDeltas(Guid assetId)
    {
        var asset = await db.Assets.FindAsync(assetId);
        if (asset is null) return NotFound();

        var deltas = await analysisService.GetHistoryAsync(assetId);
        return Ok(deltas.Select(d => ToDto(d, asset.Name)).ToArray());
    }

    // GET /api/analysis/{assetId}/latest[?skipCache=true]
    // Returns today's delta snapshot, recomputing if missing or expired.
    // Pass skipCache=true to force a fresh computation regardless of expiry.
    [HttpGet("{assetId:guid}/latest")]
    public async Task<ActionResult<DeltaDto>> GetLatest(Guid assetId, [FromQuery] bool skipCache = false)
    {
        var asset = await db.Assets.FindAsync(assetId);
        if (asset is null) return NotFound();

        var delta = await analysisService.GetLatestAsync(assetId, skipCache);
        return Ok(ToDto(delta, asset.Name));
    }

    // GET /api/analysis/{assetId}/at?date=2025-06-15
    // Returns the stored delta snapshot for a specific historical date.
    // Returns 204 No Content if no snapshot exists for that date (does not compute one).
    [HttpGet("{assetId:guid}/at")]
    public async Task<ActionResult<DeltaDto>> GetAt(Guid assetId, [FromQuery] DateTime date)
    {
        var asset = await db.Assets.FindAsync(assetId);
        if (asset is null) return NotFound();

        var row = await analysisService.GetAtAsync(assetId, date);
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

        var rows = await analysisService.GetHoldingDeltasAsync(assetId);
        if (rows.Count == 0) return Ok(Array.Empty<HoldingDto>());

        var scores = rows.Select(r => AnalysisService.Score(r.Delta)).ToArray();
        var fractions = AnalysisService.Softmax(scores);

        return Ok(rows.Select((r, i) => ToHoldingDto(r.Delta, r.Name, scores[i], fractions[i])).ToArray());
    }

    private static DeltaDto ToDto(AssetDelta d, string assetName) => new(
        d.AssetId, assetName, d.Date,
        d.MarketDelta, d.PairDelta, d.PairAssetId,
        d.PublicSentimentDelta, d.MemberSentimentDelta,
        d.FundamentalDelta, d.InstitutionalOrderFlowDelta,
        d.PatternDelta, AnalysisService.Score(d));

    private static HoldingDto ToHoldingDto(AssetDelta d, string assetName, double score, double targetFraction) => new(
        d.AssetId, assetName, d.Date,
        d.MarketDelta, d.PairDelta, d.PairAssetId,
        d.PublicSentimentDelta, d.MemberSentimentDelta,
        d.FundamentalDelta, d.InstitutionalOrderFlowDelta,
        d.PatternDelta, score, targetFraction);
}
