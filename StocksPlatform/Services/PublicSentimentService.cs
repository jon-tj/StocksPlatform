using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StocksPlatform.Services;

public record SentimentItem(string Title, string Body, string Date);
public record PublicSentiment(
    List<SentimentItem> NordnetComments,
    List<SentimentItem> NordnetNews,
    List<SentimentItem> E24News);

/// <summary>
/// Scrapes public sentiment data (user comments and news) for a stock from Nordnet and E24.
/// </summary>
/// <remarks>
/// Nordnet: provide the URL slug, e.g. "equinor-eqnr-xosl" (company-ticker-exchange).
/// E24:     provide the tag UUID associated with the stock in the e24 liveblog API.
/// </remarks>
public class PublicSentimentService(HttpClient http)
{
    private const string E24LiveblogUrl = "https://live-api-prod.schibsted.tech/e24/entries";

    public async Task<PublicSentiment> GetAsync(string nordnetSlug, string? e24Tag = null, int e24Limit = 10)
    {
        var nordnetTask = GetNordnetAsync(nordnetSlug);
        var e24Task = e24Tag is not null
            ? GetE24NewsAsync(e24Tag, e24Limit)
            : Task.FromResult(new List<SentimentItem>());

        await Task.WhenAll(nordnetTask, e24Task);
        var (comments, news) = await nordnetTask;

        return new PublicSentiment(comments, news, await e24Task);
    }

    /// <summary>Fetches the Nordnet page once and returns both comments and news.</summary>
    public async Task<(List<SentimentItem> Comments, List<SentimentItem> News)> GetNordnetAsync(string nordnetSlug)
    {
        var html = await FetchHtmlAsync($"https://www.nordnet.no/aksjer/kurser/{nordnetSlug}");
        return (ParseNordnetComments(html), ParseNordnetNews(html));
    }

    private static List<SentimentItem> ParseNordnetComments(string doc)
    {
        const string likeMarker = "aria-label=\"Lik\"";
        const string dateMarker = "text-neutral-text-weak\">";
        const string bodyMarker = "dXZQro\">";

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var comments = new List<SentimentItem>();
        int searchStart = 0;

        while (true)
        {
            int i = doc.IndexOf(likeMarker, searchStart, StringComparison.Ordinal);
            if (i == -1) break;

            // Date: last occurrence of dateMarker before the like button
            string date = string.Empty;
            int dPos = doc.LastIndexOf(dateMarker, i, StringComparison.Ordinal);
            if (dPos != -1)
            {
                int dStart = dPos + dateMarker.Length;
                var rawDate = doc[dStart..Math.Min(dStart + 40, doc.Length)];
                int dEnd = rawDate.IndexOf('<');
                date = (dEnd >= 0 ? rawDate[..dEnd] : rawDate).Trim();
            }

            // Body: last occurrence of bodyMarker before the like button, up to </span>
            string body = string.Empty;
            int bPos = doc.LastIndexOf(bodyMarker, i, StringComparison.Ordinal);
            if (bPos != -1)
            {
                int bStart = bPos + bodyMarker.Length;
                var rawBody = doc[bStart..i];
                int bEnd = rawBody.IndexOf("</span>", StringComparison.Ordinal);
                body = (bEnd >= 0 ? rawBody[..bEnd] : rawBody).Trim();
            }

            if (body.Length > 0 && seen.Add(body))
                comments.Add(new SentimentItem(string.Empty, body, date));

            searchStart = i + likeMarker.Length;
        }

        return comments;
    }

    public async Task<List<SentimentItem>> GetNordnetNewsAsync(string nordnetSlug)
    {
        var html = await FetchHtmlAsync($"https://www.nordnet.no/aksjer/kurser/{nordnetSlug}");
        return ParseNordnetNews(html);
    }

    private static List<SentimentItem> ParseNordnetNews(string html)
    {
        const string filter = "</a></span></div>";
        const string dateMark = "uppercase\">";
        var result = new List<SentimentItem>();
        foreach (var raw in FindAllBetween(html, "Les mer om ", " visninger"))
        {
            var titleEnd = raw.IndexOf('"');
            var title = (titleEnd >= 0 ? raw[..titleEnd] : raw).Trim();
            if (title.Contains(filter)) continue;

            string date = string.Empty;
            var dateStart = raw.IndexOf(dateMark);
            if (dateStart >= 0)
            {
                dateStart += dateMark.Length;
                var dateEnd = raw.IndexOf('<', dateStart);
                var slice = dateEnd >= 0
                    ? raw[dateStart..Math.Min(dateEnd, dateStart + 40)]
                    : raw[dateStart..Math.Min(dateStart + 40, raw.Length)];
                date = slice.Trim();
            }

            result.Add(new SentimentItem(title, string.Empty, date));
        }
        return result;
    }

    public async Task<List<SentimentItem>> GetE24NewsAsync(string tag, int limit = 10)
    {
        var url = $"{E24LiveblogUrl}?liveblogId=25&placement=1&limit={limit}&tag={Uri.EscapeDataString(tag)}";
        var response = await http.GetFromJsonAsync<E24LiveblogResponse>(url);
        if (response?.Entries is null) return [];

        return response.Entries
            .Select(e => new SentimentItem(
                e.Title ?? string.Empty,
                string.Join("\n", e.StructuredContent?.Components
                    .Where(c => c.Type == "text")
                    .Select(c => c.Value ?? string.Empty) ?? []),
                e.UpdatedAt ?? string.Empty))
            .ToList();
    }

    private async Task<string> FetchHtmlAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        // Nordnet returns a minimal page without a browser-like UA
        request.Headers.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Yields substrings found between every consecutive pair of <paramref name="open"/>
    /// and <paramref name="close"/> markers. Stops when a repeated result is seen if
    /// <paramref name="stopAtRepeated"/> is true.
    /// </summary>
    private static IEnumerable<string> FindAllBetween(
        string text, string open, string close, bool stopAtRepeated = true)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int start = 0;
        while (true)
        {
            int idx = text.IndexOf(open, start, StringComparison.Ordinal);
            if (idx == -1) break;
            idx += open.Length;
            int end = text.IndexOf(close, idx, StringComparison.Ordinal);
            if (end == -1) break;
            var result = text[idx..end];
            if (stopAtRepeated && !seen.Add(result)) break;
            yield return result;
            start = end + close.Length;
        }
    }

    // ── E24 API response DTOs ────────────────────────────────────────────────

    private sealed class E24LiveblogResponse
    {
        [JsonPropertyName("entries")]
        public List<E24Entry> Entries { get; set; } = [];
    }

    private sealed class E24Entry
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("updatedAt")]
        public string? UpdatedAt { get; set; }

        [JsonPropertyName("structuredContent")]
        public E24StructuredContent? StructuredContent { get; set; }
    }

    private sealed class E24StructuredContent
    {
        [JsonPropertyName("components")]
        public List<E24Component> Components { get; set; } = [];
    }

    private sealed class E24Component
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("value")]
        public string? Value { get; set; }
    }
}
