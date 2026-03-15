namespace StocksPlatform.Services.FundServices;

/// <summary>
/// Holdings data for one fund, as returned by a provider.
/// </summary>
public sealed record FundHoldingResult(
    /// <summary>Fund identifier (ISIN or unique key). Used as a key in FundPortfolioMeta.</summary>
    string FundId,
    /// <summary>Date the portfolio was last updated, formatted "yyyy-MM-dd". Null if unavailable.</summary>
    string? PortfolioDate,
    /// <summary>Individual stock/security holdings for this fund.</summary>
    IReadOnlyList<FundHoldingEntry> Holdings
);

/// <summary>One holding line from a fund portfolio.</summary>
public sealed record FundHoldingEntry(
    /// <summary>ISIN of the held security.</summary>
    string Isin,
    /// <summary>Weight of this holding as a percentage of the fund (0–100).</summary>
    double Percent
);

/// <summary>
/// Abstraction over a fund holdings data source. Implementations fetch data from
/// different providers (e.g. SpareBank 1 JSON API, HANetf XLSX files).
/// </summary>
public interface IFundHoldingsProvider
{
    /// <summary>All fund identifiers (ISINs or unique keys) this provider tracks.</summary>
    IReadOnlyList<string> FundIds { get; }

    /// <summary>
    /// Fetches current holdings for every fund this provider tracks.
    /// Failures for individual funds should be swallowed and excluded from the result.
    /// </summary>
    Task<IReadOnlyList<FundHoldingResult>> GetAllHoldingsAsync();
}
