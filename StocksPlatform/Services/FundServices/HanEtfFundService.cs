using System.Globalization;
using ClosedXML.Excel;

namespace StocksPlatform.Services.FundServices;

/// <summary>
/// Fetches fund holdings from the HANetf QUAD ETF XLSX holdings file.
///   https://hanetf.com/wp-content/assets/upload/Holdings-QUAD-IE000C7EUDG1-all-all.xlsx
///
/// The XLSX layout:
///   Rows 1–5  : header / metadata (may include an "as of" date)
///   Row 6+    : data — columns (1-indexed):
///               1  Security Description
///               2  Shares
///               3  Market Value (Base)
///               4  Trading Currency
///               5  SEDOL/CUSIP
///               6  Exposure Country
///               7  Region
///               8  ISIN
///               9  Weight  (percentage, e.g. 4.67 = 4.67 %)
/// </summary>
public class HanEtfFundService(HttpClient http) : IFundHoldingsProvider
{
    private const int IsinColumn   = 8;
    private const int WeightColumn = 9;
    private const int DataStartRow = 6;

    // Each entry is (fundIsin, holdingsUrl) — full URL, no tracking parameters.
    // hanetf.com funds use the /wp-content/assets/upload/ pattern.
    // etp.hanetf.com funds serve the file directly from a slug endpoint.
    // Source: https://hanetf.com/product-list/?investment-style=Active
    private static readonly (string Isin, string Url)[] Funds =
    [
        ("IE000C7EUDG1", "https://hanetf.com/wp-content/assets/upload/Holdings-QUAD-IE000C7EUDG1-all-all.xlsx"),
        ("IE000T8QD852", "https://hanetf.com/wp-content/assets/upload/Holdings-CHPY-IE000T8QD852-all-all.xlsx"),
        ("IE0008RSSHT4", "https://hanetf.com/wp-content/assets/upload/Holdings-UKIT-IE0008RSSHT4-all-all.xlsx"),
        ("IE000OBK3UE0", "https://etp.hanetf.com/FEGI-Holdings"),
        ("IE000AN3AFZ1", "https://hanetf.com/wp-content/assets/upload/Holdings-JOGS-IE000AN3AFZ1-all-all.xlsx"),
        ("IE0008BA4TY1", "https://etp.hanetf.com/CEGI-Holdings"),
        ("IE00BMFNW783", "https://etp.hanetf.com/TRIP-holdings"),
        ("IE00BJQTJ848", "https://etp.hanetf.com/WELL-Holdings"),
        ("IE00BMYMHS24", "https://etp.hanetf.com/AMAL-holdings"),
        ("IE00BNC1F287", "https://etp.hanetf.com/CLMA-holdings"),
        ("IE00BL643144", "https://etp.hanetf.com/ROE-holdings"),
        ("IE000MMRLY96", "https://etp.hanetf.com/Holdings-YMAG"),
        ("IE000YR7N5U8", "https://hanetf.com/wp-content/assets/upload/Holdings-OBUS-IE000YR7N5U8-all-all.xlsx"),
        ("IE000P1G9TM6", "https://etp.hanetf.com/Holdings-MCTC-IE000P1G9TM6-all-all"),
    ];

    public IReadOnlyList<string> FundIds => Funds.Select(f => f.Isin).ToArray();

    public async Task<IReadOnlyList<FundHoldingResult>> GetAllHoldingsAsync()
    {
        var results = new List<FundHoldingResult>(Funds.Length);

        foreach (var (isin, url) in Funds)
        {
            byte[] bytes;
            try { bytes = await http.GetByteArrayAsync(url); }
            catch { continue; }

            try
            {
                using var ms = new MemoryStream(bytes);
                using var workbook = new XLWorkbook(ms);
                var ws = workbook.Worksheets.First();
                var portfolioDate = ExtractPortfolioDate(ws);
                var holdings = ExtractHoldings(ws);
                results.Add(new FundHoldingResult(isin, portfolioDate, holdings));
            }
            catch { /* malformed workbook — skip */ }
        }

        return results;
    }

    /// <summary>
    /// Extracts the portfolio date from the worksheet.
    /// Checks cell A1 first for the pattern "…As Of:DD-MM-YYYY" (e.g. "Holdings As Of:12-03-2026").
    /// Falls back to scanning rows 1–5 for any DateTime-typed or parseable text cell.
    /// Returns the date as "yyyy-MM-dd", or null if nothing is found.
    /// </summary>
    private static string? ExtractPortfolioDate(IXLWorksheet ws)
    {
        // Primary: A1 "As Of:DD-MM-YYYY"
        var a1 = ws.Cell(1, 1).GetString();
        var asOfIdx = a1.IndexOf("As Of:", StringComparison.OrdinalIgnoreCase);
        if (asOfIdx >= 0)
        {
            var datePart = a1[(asOfIdx + "As Of:".Length)..].Trim();
            if (DateTime.TryParseExact(datePart, "dd-MM-yyyy", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var asOf))
                return asOf.ToString("yyyy-MM-dd");
        }

        // Fallback: scan rows 1–5 for any date value
        for (var row = 1; row <= 5; row++)
        {
            foreach (var cell in ws.Row(row).CellsUsed())
            {
                if (cell.DataType == XLDataType.DateTime)
                    return cell.GetDateTime().ToString("yyyy-MM-dd");

                var text = cell.GetString().Trim();
                if (string.IsNullOrEmpty(text)) continue;
                if (DateTime.TryParse(text, CultureInfo.InvariantCulture,
                        DateTimeStyles.AllowWhiteSpaces, out var parsed))
                    return parsed.ToString("yyyy-MM-dd");
            }
        }
        return null;
    }

    private static List<FundHoldingEntry> ExtractHoldings(IXLWorksheet ws)
    {
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? DataStartRow;
        var holdings = new List<FundHoldingEntry>();

        for (var row = DataStartRow; row <= lastRow; row++)
        {
            var isin = ws.Cell(row, IsinColumn).GetString().Trim();

            // ISINs are always 12 characters; skip blank/non-ISIN rows
            if (isin.Length != 12) continue;

            var weightCell = ws.Cell(row, WeightColumn);
            double weight;

            if (!weightCell.TryGetValue(out weight))
            {
                // Cell stored as text — strip any trailing '%' and parse
                var weightStr = weightCell.GetString().TrimEnd('%').Trim();
                if (!double.TryParse(weightStr, NumberStyles.Any, CultureInfo.InvariantCulture, out weight))
                    continue;
            }

            holdings.Add(new FundHoldingEntry(isin, weight));
        }

        return holdings;
    }
}
