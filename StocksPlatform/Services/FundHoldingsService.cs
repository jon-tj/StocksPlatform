using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StocksPlatform.Services;

/// <summary>
/// Fetches fund holdings and composition data from the SpareBank 1 open API.
///   https://www.sparebank1.no/openapi/personal/banking/fund/products/details/{isin}
/// </summary>
public class FundHoldingsService(HttpClient http)
{
    private const string BaseUrl = "https://www.sparebank1.no/openapi/personal/banking/fund/products/details";

    public async Task<FundDetails?> GetFundDetailsAsync(string isin)
    {
        var url = $"{BaseUrl}/{Uri.EscapeDataString(isin)}";
        return await http.GetFromJsonAsync<FundDetails>(url);
    }
}

public sealed class FundDetails
{
    [JsonPropertyName("isin")]
    public string Isin { get; init; } = string.Empty;

    [JsonPropertyName("inceptionDate")]
    public string? InceptionDate { get; init; }

    [JsonPropertyName("holdings")]
    public List<FundHolding> Holdings { get; init; } = [];

    [JsonPropertyName("assetAllocation")]
    public List<AssetAllocationItem> AssetAllocation { get; init; } = [];

    [JsonPropertyName("stockSectorBreakdown")]
    public List<StockSectorItem> StockSectorBreakdown { get; init; } = [];

    [JsonPropertyName("lastUpdated")]
    public FundLastUpdated? LastUpdated { get; init; }
}

public sealed class FundHolding
{
    [JsonPropertyName("externalName")]
    public string? ExternalName { get; init; }

    [JsonPropertyName("securityName")]
    public string? SecurityName { get; init; }

    [JsonPropertyName("percent")]
    public double Percent { get; init; }

    [JsonPropertyName("isin")]
    public string? Isin { get; init; }
}

public sealed class AssetAllocationItem
{
    [JsonPropertyName("percent")]
    public double Percent { get; init; }

    [JsonPropertyName("allocation")]
    public string Allocation { get; init; } = string.Empty;
}

public sealed class StockSectorItem
{
    [JsonPropertyName("percent")]
    public double Percent { get; init; }

    [JsonPropertyName("sector")]
    public string Sector { get; init; } = string.Empty;

    [JsonPropertyName("superSector")]
    public string SuperSector { get; init; } = string.Empty;
}

public sealed class FundLastUpdated
{
    [JsonPropertyName("portfolio")]
    public string? Portfolio { get; init; }

    [JsonPropertyName("price")]
    public string? Price { get; init; }
}
