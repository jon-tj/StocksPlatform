using System.Text.Json;

namespace StocksPlatform.Services.CompanyNews;

/// <summary>
/// Fetches the latest news from <c>https://www.equinor.com/en/news</c>.
///
/// The page is a Next.js SSG page — the full dataset is embedded inline in a
/// <c>&lt;script id="__NEXT_DATA__"&gt;</c> tag, so we only need one HTTP
/// request and the URL never changes.
///
/// JSON path: props → pageProps → data → response → hits[]
///   hit.pageTitle  → Title
///   hit.ingress    → Body
///   hit.publishDateTime → Date
/// </summary>
public sealed class EquinorNewsFeed(IHttpClientFactory factory, ILogger<EquinorNewsFeed> logger) : ICompanyNewsFeed
{
    /// <summary>Matches Symbol-Market for the primary Equinor listing on Oslo Stock Exchange.</summary>
    public string Key => "EQNR-XOSL";

    private HttpClient Http => factory.CreateClient("CompanyNews");

    public async Task<List<SentimentItem>> FetchAsync(int limit = 20)
    {
        logger.LogInformation("Fetching Equinor news from https://www.equinor.com/en/news");
        var html = await Http.GetStringAsync("https://www.equinor.com/en/news");

        const string open  = "<script id=\"__NEXT_DATA__\" type=\"application/json\">";
        const string close = "</script>";

        var startIdx = html.IndexOf(open, StringComparison.Ordinal);
        if (startIdx < 0) return [];
        startIdx += open.Length;

        var endIdx = html.IndexOf(close, startIdx, StringComparison.Ordinal);
        if (endIdx < 0) return [];

        using var doc = JsonDocument.Parse(html[startIdx..endIdx]);
        try
        {
            var hits = doc.RootElement
                .GetProperty("props")
                .GetProperty("pageProps")
                .GetProperty("data")
                .GetProperty("response")
                .GetProperty("hits");

            var result = new List<SentimentItem>();
            foreach (var hit in hits.EnumerateArray())
            {
                if (result.Count >= limit) break;

                var title = hit.TryGetProperty("pageTitle",       out var t) ? t.GetString() ?? "" : "";
                var body  = hit.TryGetProperty("ingress",         out var b) ? b.GetString() ?? "" : "";
                var date  = hit.TryGetProperty("publishDateTime", out var d) ? d.GetString() ?? "" : "";

                result.Add(new SentimentItem(title, body, date));
            }
            return result;
        }
        catch (KeyNotFoundException)
        {
            return [];
        }
    }
}
