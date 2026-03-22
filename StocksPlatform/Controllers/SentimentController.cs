using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;
using StocksPlatform.Services;

namespace StocksPlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SentimentController(AppDbContext db, PublicSentimentService sentiment) : ControllerBase
{
    public record SentimentItemDto(string Title, string Body, string Date);
    public record PublicSentimentDto(
        List<SentimentItemDto> NordnetComments,
        List<SentimentItemDto> NordnetNews,
        List<SentimentItemDto> E24News);

    // GET /api/sentiment/{assetId}
    // Fetches public sentiment (Nordnet comments + news, E24 news) for the given asset.
    // Uses Asset.BrokerSymbol as the Nordnet URL slug and Asset.E24Tag for E24 liveblog.
    [HttpGet("{assetId:guid}")]
    public async Task<ActionResult<PublicSentimentDto>> Get(Guid assetId, [FromQuery] int e24Limit = 10)
    {
        var asset = await db.Assets.FindAsync(assetId);
        if (asset is null) return NotFound();

        var nordnetSlug = asset.Broker?.ToLowerInvariant().Contains("nordnet") == true
            ? asset.BrokerSymbol
            : null;

        if (nordnetSlug is null && asset.E24Tag is null)
            return Ok(new PublicSentimentDto([], [], []));

        PublicSentiment result;
        if (nordnetSlug is not null)
        {
            result = await sentiment.GetAsync(nordnetSlug, asset.E24Tag, e24Limit);
        }
        else
        {
            var e24News = await sentiment.GetE24NewsAsync(asset.E24Tag!, e24Limit);
            result = new PublicSentiment([], [], e24News);
        }

        return Ok(new PublicSentimentDto(
            result.NordnetComments.Select(c => new SentimentItemDto(c.Title, c.Body, c.Date)).ToList(),
            result.NordnetNews.Select(n => new SentimentItemDto(n.Title, n.Body, n.Date)).ToList(),
            result.E24News.Select(e => new SentimentItemDto(e.Title, e.Body, e.Date)).ToList()));
    }
}
