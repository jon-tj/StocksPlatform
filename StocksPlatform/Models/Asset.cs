namespace StocksPlatform.Models;

public enum AssetType
{
    Portfolio = 0,
    Stock = 1,
    Commodity = 2,
    Crypto = 3,
    Currency = 4,
}

public class Asset
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public AssetType Type { get; set; }

    /// <summary>Internal ticker symbol, e.g. "AAPL". Null for portfolios.</summary>
    public string? Symbol { get; set; }

    /// <summary>Exchange/market, e.g. "NYSE". Null for portfolios.</summary>
    public string? Market { get; set; }

    /// <summary>Broker through which this asset is held, e.g. "NordNet".</summary>
    public string? Broker { get; set; }

    /// <summary>Symbol as it appears in the broker's system, e.g. "AAPL".</summary>
    public string? BrokerSymbol { get; set; }

    /// <summary>
    /// International Securities Identification Number (ISIN), e.g. "US67066G1040".
    /// Used to match this asset against holdings reported by fund providers.
    /// </summary>
    public string? Isin { get; set; }

    /// <summary>Broad business sector, e.g. "Technology".</summary>
    public string? Sector { get; set; }

    /// <summary>More granular industry/subsector, e.g. "Semiconductors".</summary>
    public string? Subsector { get; set; }

    /// <summary>Primary country of incorporation/listing.</summary>
    public string? Country { get; set; }

    /// <summary>Geographic region, e.g. "North America" or "Europe".</summary>
    public string? Region { get; set; }

    /// <summary>Broker-reported popularity score. Higher = more popular.</summary>
    public int? Popularity { get; set; }

    /// <summary>URL to the asset's logo/icon image.</summary>
    public string? IconUrl { get; set; }

    /// <summary>Nordnet exchange identifier UUID (NnxId), used for price history API calls.</summary>
    public string? NnxId { get; set; }

    /// <summary>Company website URL.</summary>
    public string? WebsiteUrl { get; set; }

    /// <summary>Short company description.</summary>
    public string? Description { get; set; }

    /// <summary>Chief Executive Officer name.</summary>
    public string? Ceo { get; set; }

    /// <summary>Primary address line.</summary>
    public string? Address1 { get; set; }

    /// <summary>Secondary address line.</summary>
    public string? Address2 { get; set; }

    /// <summary>Total number of shares outstanding.</summary>
    public long? NumberShares { get; set; }

    /// <summary>ISO 4217 currency code prices are denominated in, e.g. "NOK", "USD". Populated from Yahoo Finance metadata.</summary>
    public string? Currency { get; set; }
}

/// <summary>
/// Links a portfolio asset to its constituent assets.
/// The parent (PortfolioId) must be of type Portfolio.
/// The child (AssetId) can be any non-Portfolio asset type.
/// </summary>
public class PortfolioAsset
{
    public int Id { get; set; }

    public Guid PortfolioId { get; set; }
    public Asset Portfolio { get; set; } = null!;

    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;

    /// <summary>Number of units/shares held. Stored as double to allow fractional quantities (e.g. cash positions).</summary>
    public uint Quantity { get; set; }

    /// <summary>
    /// Precomputed value fraction of this asset within the portfolio (0–1).
    /// Null until first computation.
    /// </summary>
    public double? Fraction { get; set; }

    /// <summary>
    /// UTC timestamp after which Fraction should be recomputed.
    /// Null means it has never been computed.
    /// </summary>
    public DateTime? FractionExpiry { get; set; }
}

public class UserStarredAsset
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid AssetId { get; set; }
}

/// <summary>
/// Stores the uninvested cash remainder for a portfolio after pro-rata share allocation.
/// This value is added back when calculating portfolio value so rounding losses don't compound.
/// </summary>
public class PortfolioRemainderValue
{
    public int Id { get; set; }
    public Guid PortfolioId { get; set; }
    public double Value { get; set; }
}
