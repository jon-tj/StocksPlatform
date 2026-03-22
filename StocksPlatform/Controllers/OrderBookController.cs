using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StocksPlatform.Data;
using StocksPlatform.Services;

namespace StocksPlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrderBookController(AppDbContext db, OrderBookService orderBook) : ControllerBase
{
    public record OrderBookLevelDto(double Bid, double BidVol, double Ask, double AskVol);

    // GET /api/orderbook/{assetId}[?limit=20]
    // Returns live order book levels from the E24 Infront feed.
    // Only supported for XOSL (OSE) and XNAS (NAS) markets.
    [HttpGet("{assetId:guid}")]
    public async Task<ActionResult<List<OrderBookLevelDto>>> Get(Guid assetId, [FromQuery] int limit = 20)
    {
        var asset = await db.Assets.FindAsync(assetId);
        if (asset is null) return NotFound();

        var exchange = asset.Market?.ToUpperInvariant() switch
        {
            "XOSL" => "OSE",
            "XNAS" => "NAS",
            _ => null
        };

        if (exchange is null || asset.Symbol is null)
            return Ok(Array.Empty<OrderBookLevelDto>());

        var levels = await orderBook.GetOrderBookAsync(asset.Symbol, exchange, limit);
        return Ok(levels.Select(l => new OrderBookLevelDto(l.Bid, l.BidVol, l.Ask, l.AskVol)));
    }
}
