using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;
using StocksPlatform.Models;

namespace StocksPlatform.Controllers;

[ApiController]
[Route("api/orderbook")]
[Authorize]
public class OrderBookHistoryController(AppDbContext db) : ControllerBase
{
    public record OrderBookSnapshotDto(
        DateTime Timestamp,
        int Level,
        string Side,
        double Price,
        double NewVol,
        double Increment);

    // GET /api/orderbook/{assetId}/history?from=2026-03-01&to=2026-03-22&level=1&side=Bid
    // Returns persisted change records for the given asset, optionally filtered by time range,
    // depth level, and side. Intended for graphing order book volume history over time.
    [HttpGet("{assetId:guid}/history")]
    public async Task<ActionResult<List<OrderBookSnapshotDto>>> GetHistory(
        Guid assetId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int? level = null,
        [FromQuery] OrderBookSide? side = null)
    {
        var asset = await db.Assets.FindAsync(assetId);
        if (asset is null) return NotFound();

        var query = db.OrderBookSnapshots
            .Where(s => s.AssetId == assetId);

        if (from.HasValue)  query = query.Where(s => s.Timestamp >= from.Value);
        if (to.HasValue)    query = query.Where(s => s.Timestamp <= to.Value);
        if (level.HasValue) query = query.Where(s => s.Level == level.Value);
        if (side.HasValue)  query = query.Where(s => s.Side == side.Value);

        var rows = await query
            .OrderBy(s => s.Timestamp)
            .ThenBy(s => s.Level)
            .ThenBy(s => s.Side)
            .Select(s => new OrderBookSnapshotDto(
                s.Timestamp,
                s.Level,
                s.Side == OrderBookSide.Bid ? "Bid" : "Ask",
                s.Price,
                s.NewVol,
                s.Increment))
            .ToListAsync();

        return Ok(rows);
    }
}
