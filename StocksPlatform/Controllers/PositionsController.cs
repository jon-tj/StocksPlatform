using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;
using StocksPlatform.Models;
using StocksPlatform.Services;

namespace StocksPlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PositionsController(AppDbContext db, UserManager<AppUser> userManager, PollWeekService pollWeek, PriceService priceService) : ControllerBase
{
    public record PositionDto(Guid AssetId, string Symbol, string Name, uint Quantity, double? Fraction, double ReturnPercent);
    public record PositionsResponse(PositionDto[] Positions, bool Mock);

    // GET /api/positions
    [HttpGet]
    public async Task<ActionResult<PositionsResponse>> GetPositions()
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        var pollId = pollWeek.GetCurrentPollId();
        var poll = await db.Polls.FindAsync(pollId);

        if (poll is null)
        {
            // Create the poll and seed two sample questions
            poll = new Poll { Id = pollId, GeneratedDate = DateTime.UtcNow };
            db.Polls.Add(poll);
            db.PollQuestions.AddRange(
                new PollQuestion
                {
                    PollId = pollId,
                    Section = "Market Outlook",
                    Text = "Do you expect the S&P 500 to close higher this week than last week?",
                    PollType = PollQuestionType.Binary,
                },
                new PollQuestion
                {
                    PollId = pollId,
                    Section = "Market Outlook",
                    Text = "What probability do you assign to the Federal Reserve raising interest rates this month?",
                    PollType = PollQuestionType.Probability,
                }
            );
            await db.SaveChangesAsync();

            return Ok(new PositionsResponse(await GetMockPositions(), true));
        }

        // Poll exists — check if user has any response for it
        var completed = await db.PollResponses
            .AnyAsync(r => r.PollId == pollId && r.UserId == user.Id);

        return completed
            ? Ok(new PositionsResponse(await GetRealPositions(), false))
            : Ok(new PositionsResponse(await GetMockPositions(), true));
    }

    private async Task<PositionDto[]> GetRealPositions()
    {
        var now = DateTime.UtcNow;

        var rows = await db.PortfolioAssets
            .Where(pa => pa.PortfolioId == AppDbContext.PortfolioId)
            .Include(pa => pa.Asset)
            .ToListAsync();

        // Recompute fraction for any row whose expiry has passed or was never set
        var stale = rows.Where(pa => pa.FractionExpiry is null || pa.FractionExpiry <= now).ToList();
        if (stale.Count > 0)
        {
            var staleIds = stale.Select(pa => pa.AssetId);
            var prices = await priceService.GetPricesAsync(staleIds);

            // Total portfolio value = sum of (price × quantity) across ALL rows
            // For rows not being refreshed we use the cached price; for stale rows we use the newly fetched price.
            var allPrices = await priceService.GetPricesAsync(rows.Select(pa => pa.AssetId));
            var totalValue = rows.Sum(pa => (double)allPrices[pa.AssetId] * pa.Quantity);
            var expiry = now.Date.AddDays(1); // expires at midnight tomorrow UTC

            foreach (var pa in stale)
            {
                var assetValue = (double)prices[pa.AssetId] * pa.Quantity;
                pa.Fraction = totalValue > 0 ? assetValue / totalValue : 0;
                pa.FractionExpiry = expiry;
            }

            await db.SaveChangesAsync();
        }

        return rows.Select(pa => new PositionDto(
            pa.Asset.Id,
            pa.Asset.Symbol ?? pa.Asset.Name,
            pa.Asset.Name,
            pa.Quantity,
            pa.Fraction,
            0.0 // ReturnPercent placeholder — will come from AssetDelta
        )).ToArray();
    }

    private async Task<PositionDto[]> GetMockPositions()
    {
        var real = await GetRealPositions();
        // When mocked, replace symbol and name with AAPL as per spec
        return real.Select(p => p with { Symbol = "AAPL", Name = "Apple Inc." }).ToArray();
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        return email is null ? null : await userManager.FindByEmailAsync(email);
    }
}
