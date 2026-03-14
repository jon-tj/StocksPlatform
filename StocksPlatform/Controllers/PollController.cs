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
public class PollController(AppDbContext db, UserManager<AppUser> userManager, PollWeekService pollWeek) : ControllerBase
{
    public record QuestionDto(int Id, string Section, string Text, int PollType);
    public record PollDto(string PollId, QuestionDto[] Questions);
    public record AnswerDto(int QuestionId, string Value);

    // GET /api/poll/current
    [HttpGet("current")]
    public async Task<ActionResult<PollDto>> GetCurrent()
    {
        var pollId = pollWeek.GetCurrentPollId();

        var questions = await db.PollQuestions
            .Where(q => q.PollId == pollId)
            .OrderBy(q => q.Section)
            .ThenBy(q => q.Id)
            .Select(q => new QuestionDto(q.Id, q.Section, q.Text, (int)q.PollType))
            .ToListAsync();

        return Ok(new PollDto(pollId, questions.ToArray()));
    }

    // POST /api/poll/{pollId}/responses
    [HttpPost("{pollId}/responses")]
    public async Task<IActionResult> Submit(string pollId, [FromBody] AnswerDto[] answers)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        var alreadySubmitted = await db.PollResponses
            .AnyAsync(r => r.PollId == pollId && r.UserId == user.Id);

        if (alreadySubmitted) return Conflict("Poll already submitted.");

        var responses = answers.Select(a => new PollResponse
        {
            PollId = pollId,
            UserId = user.Id,
            QuestionId = a.QuestionId,
            Value = a.Value,
        });

        db.PollResponses.AddRange(responses);
        await db.SaveChangesAsync();

        return NoContent();
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        return email is null ? null : await userManager.FindByEmailAsync(email);
    }
}
