namespace StocksPlatform.Services.CompanyNews;

/// <summary>
/// Dispatches <c>FetchAsync</c> calls to the right <see cref="ICompanyNewsFeed"/>
/// by looking up <see cref="ICompanyNewsFeed.Key"/> (case-insensitive).
///
/// To add a new feed: implement <see cref="ICompanyNewsFeed"/>, register it as
/// a singleton in Program.cs, and set the asset's
/// <c>CompanyNewsFeedKey</c> to the matching key string.
/// </summary>
public sealed class CompanyNewsFeedService(IEnumerable<ICompanyNewsFeed> feeds)
{
    private readonly IReadOnlyDictionary<string, ICompanyNewsFeed> _feeds =
        feeds.ToDictionary(f => f.Key, StringComparer.OrdinalIgnoreCase);

    public bool Supports(string? symbol, string? market) =>
        symbol is not null && market is not null &&
        _feeds.ContainsKey($"{symbol}-{market}");

    public Task<List<SentimentItem>> FetchAsync(string symbol, string market, int limit = 20)
    {
        var key = $"{symbol}-{market}";
        return _feeds.TryGetValue(key, out var feed)
            ? feed.FetchAsync(limit)
            : Task.FromResult<List<SentimentItem>>([]);
    }
}
