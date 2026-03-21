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
public class PositionsController(AppDbContext db, UserManager<AppUser> userManager, PollWeekService pollWeek, FractionService fractionService) : ControllerBase
{
    public record PositionDto(Guid AssetId, string Symbol, string Name, uint Quantity, double? Fraction, double ReturnPercent, string? IconUrl, string? Sector, string? Region);
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
        var rows = await fractionService.GetFreshChildrenAsync(AppDbContext.MainPortfolioId);

        return rows.Select(pa => new PositionDto(
            pa.Asset.Id,
            pa.Asset.Symbol ?? pa.Asset.Name,
            pa.Asset.Name,
            pa.Quantity,
            pa.Fraction,
            0.0, // ReturnPercent placeholder — will come from AssetDelta
            pa.Asset.IconUrl,
            pa.Asset.Sector,
            pa.Asset.Region
        )).ToArray();
    }

    // GET /api/positions/{portfolioId}
    // Returns children of any portfolio, without poll gate (used by the analysis view).
    [HttpGet("{portfolioId:guid}")]
    public async Task<ActionResult<PositionDto[]>> GetPortfolioPositions(Guid portfolioId)
    {
        var asset = await db.Assets.FindAsync(portfolioId);
        if (asset is null) return NotFound();
        if (asset.Type != AssetType.Portfolio) return BadRequest("Asset is not a portfolio.");

        var rows = await fractionService.GetFreshChildrenAsync(portfolioId);
        return Ok(rows.Select(pa => new PositionDto(
            pa.Asset.Id,
            pa.Asset.Symbol ?? pa.Asset.Name,
            pa.Asset.Name,
            pa.Quantity,
            pa.Fraction,
            0.0,
            pa.Asset.IconUrl,
            pa.Asset.Sector,
            pa.Asset.Region
        )).ToArray());
    }

    private async Task<PositionDto[]> GetMockPositions()
    {
        var real = await GetRealPositions();
        // When mocked, replace symbol and name with AAPL as per spec
        return real.Select(p => p with { Symbol = "AAPL", Name = "Apple Inc." }).ToArray();
    }

    // GET /api/positions/{portfolioId}/remainder
    [HttpGet("{portfolioId:guid}/remainder")]
    public async Task<ActionResult<double>> GetPortfolioRemainder(Guid portfolioId)
    {
        var row = await db.PortfolioRemainderValues.FirstOrDefaultAsync(r => r.PortfolioId == portfolioId);
        return Ok(row?.Value ?? 0.0);
    }

    // PUT /api/positions/{portfolioId}/remainder
    [HttpPut("{portfolioId:guid}/remainder")]
    public async Task<IActionResult> SetPortfolioRemainder(Guid portfolioId, [FromBody] double value)
    {
        var row = await db.PortfolioRemainderValues.FirstOrDefaultAsync(r => r.PortfolioId == portfolioId);
        if (row is null)
        {
            db.PortfolioRemainderValues.Add(new PortfolioRemainderValue { PortfolioId = portfolioId, Value = value });
        }
        else
        {
            row.Value = value;
        }
        await db.SaveChangesAsync();
        return Ok();
    }

    public record UpdateQuantityDto(Guid AssetId, int Quantity);

    // PATCH /api/positions/{portfolioId}/quantities
    // Updates the Quantity of each specified child asset in the portfolio and invalidates fractions.
    [HttpPatch("{portfolioId:guid}/quantities")]
    public async Task<IActionResult> UpdatePortfolioQuantities(Guid portfolioId, [FromBody] List<UpdateQuantityDto> updates)
    {
        var asset = await db.Assets.FindAsync(portfolioId);
        if (asset is null) return NotFound();
        if (asset.Type != AssetType.Portfolio) return BadRequest("Asset is not a portfolio.");

        var rows = await db.PortfolioAssets
            .Where(pa => pa.PortfolioId == portfolioId)
            .ToListAsync();

        foreach (var update in updates)
        {
            var pa = rows.FirstOrDefault(p => p.AssetId == update.AssetId);
            if (pa is not null)
            {
                pa.Quantity = (uint)Math.Max(0, update.Quantity);
                pa.FractionExpiry = null; // invalidate so fractions recompute on next fetch
            }
        }

        await db.SaveChangesAsync();
        return Ok();
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        return email is null ? null : await userManager.FindByEmailAsync(email);
    }
}
