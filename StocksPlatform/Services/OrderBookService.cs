using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StocksPlatform.Services;

public record OrderBookLevel(double Bid, double BidVol, double Ask, double AskVol);

/// <summary>
/// Fetches the live order book from the E24 Infront data feed.
/// Supported exchanges: OSE (Oslo Stock Exchange) and NAS (NASDAQ via e24).
/// </summary>
public class OrderBookService(HttpClient http)
{
    private const string BaseUrl = "https://api.e24.no/bors/infront/server/components";

    /// <summary>
    /// Returns order book levels for a given symbol and E24 exchange suffix (e.g. "OSE").
    /// Levels are returned sorted by price proximity (best bid/ask first).
    /// </summary>
    public async Task<List<OrderBookLevel>> GetOrderBookAsync(string symbol, string exchange, int limit = 20)
    {
        // E24 filter uses the form sEQNR.OSE
        var filter = $"ITEM_SECTOR=={Uri.EscapeDataString($"s{symbol}.{exchange}")}";
        var url = $"{BaseUrl}?source=feed.ose.orders.EQUITIES+as+source" +
                  $"&limit={limit}" +
                  $"&columns=level%2CASK%2CBID%2CASKVOL%2CBIDVOL" +
                  $"&convert=levels" +
                  $"&filter={filter}";

        var response = await http.GetFromJsonAsync<InfrontResponse>(url);
        if (response?.Rows is null) return [];

        return response.Rows
            .Select(r => new OrderBookLevel(
                r.Values.GetValueOrDefault("BID"),
                r.Values.GetValueOrDefault("BIDVOL"),
                r.Values.GetValueOrDefault("ASK"),
                r.Values.GetValueOrDefault("ASKVOL")))
            .ToList();
    }

    // ── E24 Infront API response DTOs ────────────────────────────────────────

    private sealed class InfrontResponse
    {
        [JsonPropertyName("rows")]
        public List<InfrontRow> Rows { get; set; } = [];
    }

    private sealed class InfrontRow
    {
        [JsonPropertyName("values")]
        public Dictionary<string, double> Values { get; set; } = [];
    }
}
