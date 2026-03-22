namespace StocksPlatform.Services.CompanyNews;

/// <summary>
/// Implemented once per company whose news site / API has a dedicated scraper.
/// Register each implementation as <c>ICompanyNewsFeed</c> in DI, then set
/// <see cref="Models.Asset.CompanyNewsFeedKey"/> to whichever <see cref="Key"/>
/// string matches.
/// </summary>
public interface ICompanyNewsFeed
{
    /// <summary>Lookup key in the form "SYMBOL-MARKET", e.g. "EQNR-XOSL".</summary>
    string Key { get; }

    Task<List<SentimentItem>> FetchAsync(int limit = 20);
}
