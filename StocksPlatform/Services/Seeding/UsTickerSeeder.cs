using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;
using StocksPlatform.Models;

namespace StocksPlatform.Services.Seeding;

/// <summary>
/// Idempotently seeds major US large-cap stocks into the Assets table.
/// This allows fund holdings matched by US ISINs to resolve against known assets.
/// </summary>
public static class UsTickerSeeder
{
    // (Symbol, Display name, Exchange, ISIN, Sector, Subsector)
    private static readonly (string Symbol, string Name, string BrokerSymbol, string Exchange, string Isin, string Sector, string Subsector)[] Tickers =
    [
        ("AAPL", "Apple Inc.", "aaple-aapl-xnas", "NASDAQ", "US0378331005", "Technology", "Consumer Electronics"),
        ("MSFT", "Microsoft Corp.", "msft-microsoft-xnas", "NASDAQ", "US5949181045", "Technology", "Software"),
        ("NVDA", "NVIDIA Corp.", "nvda-nvidia-xnas", "NASDAQ", "US67066G1040", "Technology", "Semiconductors"),
        ("AMZN", "Amazon.com Inc.", "amzn-amazon-xnas", "NASDAQ", "US0231351067", "Consumer Cyclical", "Internet Retail"),
        ("META", "Meta Platforms", "meta-meta-xnas", "NASDAQ", "US30303M1027", "Communication Services", "Internet Content & Information"),
        ("GOOGL", "Alphabet Inc. Class A", "alphabet-a-googl-xnas", "NASDAQ", "US02079K3059", "Communication Services", "Internet Content & Information"),
        ("GOOG", "Alphabet Inc. Class C", "alphabet-c-goog-xnas", "NASDAQ", "US02079K1079", "Communication Services", "Internet Content & Information"),
        ("TSLA", "Tesla Inc.", "tesla-tsla-xnas", "NASDAQ", "US88160R1014", "Consumer Cyclical", "Auto Manufacturers"),
        ("BRK.B", "Berkshire Hathaway Class B", "berkshire-hathaway-b-brk0b-xnas", "NYSE", "US0846707026", "Financial Services", "Insurance - Diversified"),
        ("JPM", "JPMorgan Chase & Co.", "jp-morgan-chase-jpm-xnas", "NYSE", "US46625H1005", "Financial Services", "Banks - Diversified"),
        ("V", "Visa Inc.", "visa-v-xnas", "NYSE", "US92826C8394", "Financial Services", "Credit Services"),
        ("MA", "Mastercard Inc.", "mastercard-ma-xnas", "NYSE", "US57636Q1040", "Financial Services", "Credit Services"),
        ("WMT", "Walmart Inc.", "walmart-wmt-xnas", "NYSE", "US9311421039", "Consumer Defensive", "Discount Stores"),
        ("XOM", "Exxon Mobil Corp.", "exxon-mobil-xom-xnas", "NYSE", "US30231G1022", "Energy", "Integrated Oil & Gas"),
        ("ORCL", "Oracle Corp.", "oracle-orcl-xnas", "NYSE", "US68389X1054", "Technology", "Software"),
        ("NFLX", "Netflix Inc.", "netflix-nflx-xnas", "NASDAQ", "US64110L1061", "Communication Services", "Entertainment"),
        ("COST", "Costco Wholesale Corp.", "costco-cost-xnas", "NASDAQ", "US22160K1051", "Consumer Defensive", "Discount Stores"),
        ("KO", "Coca-Cola Co.", "coca-cola-ko-xnas", "NYSE", "US1912161007", "Consumer Defensive", "Beverages - Non-Alcoholic"),
        ("PG", "Procter & Gamble Co.", "procter-gamble-pg-xnas", "NYSE", "US7427181091", "Consumer Defensive", "Household & Personal Products"),
        ("AVGO", "Broadcom Inc.", "broadcom-avgo-xnas", "NASDAQ", "US11135F1012", "Technology", "Semiconductors"),
        ("HD", "Home Depot Inc.", "home-depot-hd-xnas", "NYSE", "US4370761029", "Consumer Cyclical", "Home Improvement Retail"),
        ("ABBV", "AbbVie Inc.", "abbvie-abbv-xnas", "NYSE", "US00287Y1091", "Health Care", "Drug Manufacturers"),
        ("BAC", "Bank of America Corp.", "bank-america-bac-xnas", "NYSE", "US0605051046", "Financial Services", "Banks - Diversified"),
        ("MRK", "Merck & Co. Inc.", "merck-mrk-xnas", "NYSE", "US58933Y1055", "Health Care", "Drug Manufacturers"),
        ("PEP", "PepsiCo Inc.", "pepsico-pep-xnas", "NASDAQ", "US7134481081", "Consumer Defensive", "Beverages - Non-Alcoholic"),
        ("ADBE", "Adobe Inc.", "adobe-adbe-xnas", "NASDAQ", "US00724F1012", "Technology", "Software"),
        ("CRM", "Salesforce Inc.", "salesforce-crm-xnas", "NYSE", "US79466L3024", "Technology", "Software"),
        ("AMD", "Advanced Micro Devices", "advanced-micro-devices-amd-xnas", "NASDAQ", "US0079031078", "Technology", "Semiconductors"),
        ("CSCO", "Cisco Systems Inc.", "cisco-systems-csco-xnas", "NASDAQ", "US17275R1023", "Technology", "Communication Equipment"),
        ("INTC", "Intel Corp.", "intel-intc-xnas", "NASDAQ", "US4581401001", "Technology", "Semiconductors"),
        ("QCOM", "Qualcomm Inc.", "qualcomm-qcom-xnas", "NASDAQ", "US7475251036", "Technology", "Semiconductors"),
        ("AMAT", "Applied Materials Inc.", "applied-materials-amat-xnas", "NASDAQ", "US0382221051", "Technology", "Semiconductor Equipment"),
        ("TXN", "Texas Instruments Inc.", "texas-instruments-txn-xnas", "NASDAQ", "US8825081040", "Technology", "Semiconductors"),
        ("IBM", "IBM", "ibm-xnas", "NYSE", "US4592001014", "Technology", "Information Technology Services"),
        ("GE", "GE Aerospace", "ge-aerospace-xnas", "NYSE", "US3696043013", "Industrials", "Aerospace & Defense"),
        ("CAT", "Caterpillar Inc.", "caterpillar-cat-xnas", "NYSE", "US1491231015", "Industrials", "Farm & Heavy Construction Machinery"),
        ("UNH", "UnitedHealth Group Inc.", "united-health-group-unh-xnas", "NYSE", "US91324P1021", "Health Care", "Healthcare Plans"),
        ("PFE", "Pfizer Inc.", "pfizer-pfe-xnas", "NYSE", "US7170811035", "Health Care", "Drug Manufacturers"),
        ("LLY", "Eli Lilly and Co.", "eli-lilly-llly-xnas", "NYSE", "US5324571083", "Health Care", "Drug Manufacturers"),
        ("CVX", "Chevron Corp.", "chevron-cvx-xnas", "NYSE", "US1667641005", "Energy", "Integrated Oil & Gas"),
        ("MCD", "McDonald's Corp.", "mcdonalds-mcd-xnas", "NYSE", "US5801351017", "Consumer Cyclical", "Restaurants"),
        ("DIS", "Walt Disney Co.", "walt-disney-dis-xnas", "NYSE", "US2546871060", "Communication Services", "Entertainment"),
        ("T", "AT&T Inc.", "att-t-xnas", "NYSE", "US00206R1023", "Communication Services", "Telecom Services"),
        ("VZ", "Verizon Communications", "verizon-communications-vz-xnas", "NYSE", "US92343V1044", "Communication Services", "Telecom Services"),
        ("CMCSA", "Comcast Corp.", "comcast-cmcsa-xnas", "NASDAQ", "US20030N1019", "Communication Services", "Telecom Services"),
        ("NKE", "Nike Inc.", "nike-nke-xnys", "NYSE", "US6541061031", "Consumer Cyclical", "Footwear & Accessories"),
        ("UPS", "United Parcel Service", "ups-ups-xnys", "NYSE", "US9113121068", "Industrials", "Integrated Freight & Logistics"),
        ("FDX", "FedEx Corp.", "fedex-fdx-xnys", "NYSE", "US31428X1063", "Industrials", "Integrated Freight & Logistics"),
        ("RTX", "RTX Corp.", "rtx-rtx-xnys", "NYSE", "US75513E1010", "Industrials", "Aerospace & Defense"),
        ("BA", "Boeing Co.", "boeing-ba-xnys", "NYSE", "US0970231058", "Industrials", "Aerospace & Defense"),
        ("SPGI", "S&P Global Inc.", "sp-global-spgi-xnas", "NYSE", "US78409V1044", "Financial Services", "Financial Data & Exchanges"),
        ("MS", "Morgan Stanley", "morgan-stanley-ms-xnys", "NYSE", "US6174464486", "Financial Services", "Capital Markets"),
        ("GS", "Goldman Sachs Group Inc.", "goldman-sachs-gs-xnys", "NYSE", "US38141G1040", "Financial Services", "Capital Markets"),
        ("BLK", "BlackRock Inc.", "blackrock-blk-xnys", "NYSE", "US09247X1019", "Financial Services", "Asset Management"),
        ("BKNG", "Booking Holdings Inc.", "booking-holdings-bkng-xnas", "NASDAQ", "US09857L1089", "Consumer Cyclical", "Travel Services"),
        ("UBER", "Uber Technologies Inc.", "uber-uber-xnys", "NYSE", "US90353T1007", "Technology", "Software"),
        ("PANW", "Palo Alto Networks", "palo-alto-networks-panw-xnas", "NASDAQ", "US6974351057", "Technology", "Software - Infrastructure"),
        ("CRWD", "CrowdStrike Holdings", "crowdstrike-crwd-xnas", "NASDAQ", "US22788C1053", "Technology", "Software - Infrastructure"),
        ("NOW", "ServiceNow Inc.", "servicenow-now-xnys", "NYSE", "US81762P1021", "Technology", "Software"),
        ("ADP", "Automatic Data Processing", "automatic-data-processing-adp-xnas", "NASDAQ", "US0530151036", "Industrials", "Staffing & Employment Services"),
        ("INTU", "Intuit Inc.", "intuit-intu-xnas", "NASDAQ", "US4612021034", "Technology", "Software"),
        ("AMGN", "Amgen Inc.", "amgen-amgn-xnas", "NASDAQ", "US0311621009", "Health Care", "Drug Manufacturers"),
        ("GILD", "Gilead Sciences Inc.", "gilead-sciences-gild-xnas", "NASDAQ", "US3755581036", "Health Care", "Drug Manufacturers"),
        ("ABT", "Abbott Laboratories", "abbott-laboratories-abt-xnys", "NYSE", "US0028241000", "Health Care", "Medical Devices"),
        ("TMO", "Thermo Fisher Scientific", "thermo-fisher-scientific-tmo-xnys", "NYSE", "US8835561023", "Health Care", "Diagnostics & Research"),
        ("DHR", "Danaher Corp.", "danaher-dhr-xnys", "NYSE", "US2358511028", "Health Care", "Diagnostics & Research"),
        ("LRCX", "Lam Research Corp.", "lam-research-lrcx-xnas", "NASDAQ", "US5128071082", "Technology", "Semiconductor Equipment"),
        ("MU", "Micron Technology", "micron-mu-xnas", "NASDAQ", "US5951121038", "Technology", "Semiconductors"),
        ("SBUX", "Starbucks Corp.", "starbucks-sbux-xnas", "NASDAQ", "US8552441094", "Consumer Cyclical", "Restaurants"),
        ("LOW", "Lowe's Cos. Inc.", "lowe-s-low-xnas", "NYSE", "US5486611073", "Consumer Cyclical", "Home Improvement Retail"),
        ("DE", "Deere & Co.", "deere-co0-de-xnas", "NYSE", "US2441991054", "Industrials", "Farm & Heavy Construction Machinery"),
        ("CB", "Chubb Ltd.", "chubb-cb-xnas", "NYSE", "US1255231003", "Financial Services", "Insurance"),
        ("AXP", "American Express", "american-express-axp-xnys", "NYSE", "US0258161092", "Financial Services", "Credit Services"),
        ("C", "Citigroup Inc.", "citigroup-c-xnys", "NYSE", "US1729674242", "Financial Services", "Banks - Diversified"),
        ("WFC", "Wells Fargo & Co.", "wells-fargo-wfc-xnys", "NYSE", "US9497461015", "Financial Services", "Banks - Diversified"),
        ("SCHW", "Charles Schwab Corp.", "charles-schwab-schw-xnys", "NYSE", "US8085131055", "Financial Services", "Capital Markets"),
        ("PYPL", "PayPal Holdings", "paypal-pypl-xnas", "NASDAQ", "US70450Y1038", "Financial Services", "Credit Services"),
        ("PLTR", "Palantir Technologies", "palantir-pltr-xnas", "NASDAQ", "US69608A1088", "Technology", "Software - Infrastructure"),
        ("SNOW", "Snowflake Inc.", "snowflake-snow-xnys", "NYSE", "US8334451098", "Technology", "Software"),
    ];

    public static async Task SeedAsync(AppDbContext db)
    {
        var existingAssets = await db.Assets
            .Where(a => a.Market == "NASDAQ" || a.Market == "NYSE")
            .ToListAsync();

        var existingBySymbol = existingAssets
            .Where(a => a.Symbol != null)
            .ToDictionary(a => a.Symbol!, StringComparer.OrdinalIgnoreCase);

        var toInsert = new List<Asset>();

        foreach (var t in Tickers)
        {
            if (existingBySymbol.TryGetValue(t.Symbol, out var existing))
            {
                if (string.IsNullOrWhiteSpace(existing.Isin)) existing.Isin = t.Isin;
                if (string.IsNullOrWhiteSpace(existing.Sector)) existing.Sector = t.Sector;
                if (string.IsNullOrWhiteSpace(existing.Subsector)) existing.Subsector = t.Subsector;
                if (string.IsNullOrWhiteSpace(existing.Country)) existing.Country = "United States";
                if (string.IsNullOrWhiteSpace(existing.Region)) existing.Region = "North America";
                if (string.IsNullOrWhiteSpace(existing.Market)) existing.Market = t.Exchange;
                existing.Broker = "NordNet";
                existing.BrokerSymbol = t.BrokerSymbol;
            }
            else
            {
                toInsert.Add(new Asset
                {
                    Id = AppDbContext.AssetGuid(t.Symbol),
                    Name = t.Name,
                    Type = AssetType.Stock,
                    Symbol = t.Symbol,
                    Market = t.Exchange,
                    Broker = "NordNet",
                    BrokerSymbol = t.BrokerSymbol,
                    Isin = t.Isin,
                    Sector = t.Sector,
                    Subsector = t.Subsector,
                    Country = "United States",
                    Region = "North America",
                });
            }
        }

        if (toInsert.Count > 0)
            db.Assets.AddRange(toInsert);

        await db.SaveChangesAsync();
    }
}
