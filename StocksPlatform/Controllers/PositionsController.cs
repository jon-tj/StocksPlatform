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
public class PositionsController(AppDbContext db, UserManager<AppUser> userManager, PollWeekService pollWeek) : ControllerBase
{
    public record PositionDto(string Symbol, double SharesFraction, double ReturnPercent);
    public record PositionsResponse(PositionDto[] Positions, bool Mock);

    private static readonly PositionDto[] RealPositions =
    [
        new("NVDA", 18.4, 12.3),
        new("MSFT", 14.7,  6.8),
        new("AAPL", 12.1,  3.2),
        new("META",  9.8, 15.1),
        new("AMZN",  8.3,  7.4),
    ];

    // When mocked, all tickers replaced with AAPL as per spec
    private static PositionDto[] MockPositions() =>
        RealPositions.Select(p => p with { Symbol = "AAPL" }).ToArray();

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
            // Create the poll so the user can discover and complete it
            poll = new Poll { Id = pollId, GeneratedDate = DateTime.UtcNow };
            db.Polls.Add(poll);
            await db.SaveChangesAsync();

            return Ok(new PositionsResponse(MockPositions(), true));
        }

        // Poll exists — check if user has any response for it
        var completed = await db.PollResponses
            .AnyAsync(r => r.PollId == pollId && r.UserId == user.Id);

        return completed
            ? Ok(new PositionsResponse(RealPositions, false))
            : Ok(new PositionsResponse(MockPositions(), true));
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        return email is null ? null : await userManager.FindByEmailAsync(email);
    }
}
