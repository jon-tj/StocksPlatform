namespace StocksPlatform.Models;

public enum PollQuestionType
{
    Binary = 0,
    Probability = 1,
}

public class Poll
{
    /// <summary>Week code, e.g. "2612" = week 12 of 2026 (ISO Monday-based).</summary>
    public string Id { get; set; } = string.Empty;
    public DateTime GeneratedDate { get; set; }
}

public class PollQuestion
{
    public int Id { get; set; }
    public string PollId { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public PollQuestionType PollType { get; set; }
}

public class PollResponse
{
    public int Id { get; set; }
    public string PollId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public int QuestionId { get; set; }
    public string Value { get; set; } = string.Empty;
}
