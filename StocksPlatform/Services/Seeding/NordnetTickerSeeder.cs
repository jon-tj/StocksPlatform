using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;
using StocksPlatform.Models;
using System.Reflection;

namespace StocksPlatform.Services.Seeding;

/// <summary>
/// Seeds European stocks from the embedded nordnet_all_stocks.csv into the Assets table.
/// Sector/subsector is resolved from a pre-known ISIN lookup; for unknowns, keyword
/// matching on the company name provides a best-effort sector with an empty subsector.
/// Safe to run on every startup (idempotent).
/// </summary>
public static class NordnetTickerSeeder
{
    /// <summary>ISIN → (Sector, Subsector) for well-known European stocks.</summary>
    private static readonly Dictionary<string, (string Sector, string Subsector)> KnownSectors =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // --- Energy ---
            ["FR0000120271"] = ("Energy", "Integrated Oil & Gas"),              // TotalEnergies
            ["GB00BP6MXD84"] = ("Energy", "Integrated Oil & Gas"),              // Shell
            ["IT0003132476"] = ("Energy", "Integrated Oil & Gas"),              // Eni
            ["ES0173516115"] = ("Energy", "Integrated Oil & Gas"),              // Repsol
            ["PTGAL0AM0009"] = ("Energy", "Oil & Gas E&P"),                     // GALP
            ["LU2598331598"] = ("Energy", "Oil & Gas Equipment & Services"),    // Tenaris
            ["IT0005495657"] = ("Energy", "Oil & Gas Equipment & Services"),    // Saipem

            // --- Utilities ---
            ["FR0010208488"] = ("Utilities", "Utilities - Diversified"),        // Engie
            ["DE0007037129"] = ("Utilities", "Utilities - Diversified"),        // RWE
            ["DE000ENAG999"] = ("Utilities", "Utilities - Regulated Electric"), // E.On
            ["IT0003128367"] = ("Utilities", "Utilities - Regulated Electric"), // Enel
            ["ES0144580Y14"] = ("Utilities", "Utilities - Regulated Electric"), // Iberdrola
            ["PTEDP0AM0009"] = ("Utilities", "Utilities - Regulated Electric"), // EDP
            ["BE0003822393"] = ("Utilities", "Utilities - Regulated Electric"), // Elia Group
            ["ES0116870314"] = ("Utilities", "Utilities - Regulated Gas"),      // Naturgy
            ["IT0003153415"] = ("Utilities", "Utilities - Regulated Gas"),      // Snam
            ["IT0005211237"] = ("Utilities", "Utilities - Regulated Gas"),      // Italgas
            ["FR0000124141"] = ("Utilities", "Waste Management"),               // Veolia

            // --- Technology ---
            ["NL0010273215"] = ("Technology", "Semiconductor Equipment"),       // ASML
            ["NL0000334118"] = ("Technology", "Semiconductor Equipment"),       // ASM International
            ["DE000A0WMPJ6"] = ("Technology", "Semiconductor Equipment"),       // AIXTRON
            ["NL0012866412"] = ("Technology", "Semiconductors"),                // BE Semiconductor
            ["DE0006231004"] = ("Technology", "Semiconductors"),                // Infineon
            ["NL0000226223"] = ("Technology", "Semiconductors"),                // STMicroelectronics
            ["FR0013227113"] = ("Technology", "Semiconductors"),                // SOITEC
            ["DE0007164600"] = ("Technology", "Software"),                      // SAP
            ["NL0012969182"] = ("Technology", "Software"),                      // ADYEN
            ["FR0014003TT8"] = ("Technology", "Software"),                      // Dassault Systemes
            ["ES0109067019"] = ("Technology", "Software"),                      // Amadeus IT
            ["NL0013654783"] = ("Technology", "Internet Content & Information"), // Prosus
            ["NL0000395903"] = ("Technology", "Information Technology Services"), // Wolters Kluwer
            ["FR0000125338"] = ("Technology", "Information Technology Services"), // Capgemini
            ["ES0118594417"] = ("Technology", "Information Technology Services"), // Indra Sistemas

            // --- Communication Services ---
            ["DE0005557508"] = ("Communication Services", "Telecom Services"),  // Deutsche Telekom
            ["FR0000133308"] = ("Communication Services", "Telecom Services"),  // Orange
            ["ES0178430E18"] = ("Communication Services", "Telecom Services"),  // Telefonica
            ["NL0000009082"] = ("Communication Services", "Telecom Services"),  // KPN
            ["ES0105066007"] = ("Communication Services", "Telecom Services"),  // Cellnex
            ["IT0003497168"] = ("Communication Services", "Telecom Services"),  // Telecom Italia
            ["FR0000130577"] = ("Communication Services", "Advertising Agencies"), // Publicis
            ["NL0015000IY2"] = ("Communication Services", "Entertainment"),     // Universal Music Group

            // --- Industrials ---
            ["DE000ENER6Y0"] = ("Industrials", "Electrical Equipment"),         // Siemens Energy
            ["NL0000235190"] = ("Industrials", "Aerospace & Defense"),          // Airbus
            ["DE0007030009"] = ("Industrials", "Aerospace & Defense"),          // Rheinmetall
            ["FR0000073272"] = ("Industrials", "Aerospace & Defense"),          // Safran
            ["FR0000121329"] = ("Industrials", "Aerospace & Defense"),          // Thales
            ["DE000A0D9PT0"] = ("Industrials", "Aerospace & Defense"),          // MTU Aero Engines
            ["IT0003856405"] = ("Industrials", "Aerospace & Defense"),          // Leonardo
            ["FR0014004L86"] = ("Industrials", "Aerospace & Defense"),          // Dassault Aviation
            ["DE000HAG0005"] = ("Industrials", "Aerospace & Defense"),          // Hensoldt
            ["DE000RENK730"] = ("Industrials", "Aerospace & Defense"),          // RENK Group
            ["DE0007236101"] = ("Industrials", "Industrial Conglomerates"),     // Siemens AG
            ["DE0007500001"] = ("Industrials", "Industrial Conglomerates"),     // thyssenkrupp
            ["FR0000121972"] = ("Industrials", "Electrical Equipment"),         // Schneider Electric
            ["FR0010307819"] = ("Industrials", "Electrical Equipment"),         // Legrand
            ["IT0004176001"] = ("Industrials", "Electrical Equipment"),         // Prysmian
            ["CH0012221716"] = ("Industrials", "Electrical Equipment"),         // ABB
            ["FR0010451203"] = ("Industrials", "Electrical Equipment"),         // Rexel
            ["DE0005552004"] = ("Industrials", "Integrated Freight & Logistics"), // Deutsche Post / DHL
            ["LU2290522684"] = ("Industrials", "Integrated Freight & Logistics"), // INPOST
            ["DE0008232125"] = ("Industrials", "Airlines"),                     // Lufthansa
            ["FR001400J770"] = ("Industrials", "Airlines"),                     // Air France-KLM
            ["ES0177542018"] = ("Industrials", "Airlines"),                     // IAG
            ["FR0000125486"] = ("Industrials", "Engineering & Construction"),   // Vinci
            ["NL0015001FS8"] = ("Industrials", "Engineering & Construction"),   // Ferrovial
            ["FR0000130452"] = ("Industrials", "Engineering & Construction"),   // Eiffage
            ["FR0000120503"] = ("Industrials", "Engineering & Construction"),   // Bouygues
            ["DE0006070006"] = ("Industrials", "Engineering & Construction"),   // Hochtief
            ["FR0012757854"] = ("Industrials", "Engineering & Construction"),   // SPIE
            ["FR0000125007"] = ("Industrials", "Building Products & Equipment"), // Saint-Gobain
            ["DE000DTR0CK8"] = ("Industrials", "Farm & Heavy Construction Machinery"), // Daimler Truck
            ["DE000KGX8881"] = ("Industrials", "Farm & Heavy Construction Machinery"), // Kion Group
            ["FR0006174348"] = ("Industrials", "Specialty Business Services"), // Bureau Veritas
            ["GB00B2B0DG97"] = ("Industrials", "Specialty Business Services"), // RELX
            ["ES0105046017"] = ("Industrials", "Airports & Air Services"),      // Aena

            // --- Financial Services ---
            ["IT0005239360"] = ("Financial Services", "Banks - Diversified"),   // UniCredit
            ["NL0011821202"] = ("Financial Services", "Banks - Diversified"),   // ING
            ["FR0000130809"] = ("Financial Services", "Banks - Diversified"),   // Societe Generale
            ["FR0000131104"] = ("Financial Services", "Banks - Diversified"),   // BNP Paribas
            ["DE0005140008"] = ("Financial Services", "Banks - Diversified"),   // Deutsche Bank
            ["FR0000045072"] = ("Financial Services", "Banks - Diversified"),   // Credit Agricole
            ["IT0000072618"] = ("Financial Services", "Banks - Diversified"),   // Intesa Sanpaolo
            ["ES0113900J37"] = ("Financial Services", "Banks - Diversified"),   // Santander
            ["ES0113211835"] = ("Financial Services", "Banks - Diversified"),   // BBVA
            ["DE000CBK1001"] = ("Financial Services", "Banks - Diversified"),   // Commerzbank
            ["NL0011540547"] = ("Financial Services", "Banks - Diversified"),   // ABN AMRO
            ["AT0000652011"] = ("Financial Services", "Banks - Diversified"),   // Erste Group
            ["BE0003565737"] = ("Financial Services", "Banks - Diversified"),   // KBC
            ["PTBCP0AM0015"] = ("Financial Services", "Banks - Diversified"),   // BCP
            ["ES0140609019"] = ("Financial Services", "Banks - Regional"),      // CaixaBank
            ["ES0113679I37"] = ("Financial Services", "Banks - Regional"),      // Bankinter
            ["IT0005218380"] = ("Financial Services", "Banks - Regional"),      // Banco BPM
            ["IT0000066123"] = ("Financial Services", "Banks - Regional"),      // BPER Banca
            ["IT0005508921"] = ("Financial Services", "Banks - Regional"),      // Banca Monte dei Paschi
            ["IT0000072170"] = ("Financial Services", "Banks - Regional"),      // FinecoBank
            ["DE0008404005"] = ("Financial Services", "Insurance - Diversified"), // Allianz
            ["FR0000120628"] = ("Financial Services", "Insurance - Diversified"), // AXA
            ["IT0000062072"] = ("Financial Services", "Insurance - Diversified"), // Generali
            ["NL0010773842"] = ("Financial Services", "Insurance - Diversified"), // NN Group
            ["NL0011872643"] = ("Financial Services", "Insurance - Diversified"), // ASR Nederland
            ["BMG0112X1056"] = ("Financial Services", "Insurance - Diversified"), // AEGON
            ["IT0003796171"] = ("Financial Services", "Insurance - Diversified"), // Poste Italiane
            ["DE0008430026"] = ("Financial Services", "Insurance - Reinsurance"), // Munich Re
            ["DE0008402215"] = ("Financial Services", "Insurance - Reinsurance"), // Hannover Re
            ["CH0244767585"] = ("Financial Services", "Capital Markets"),       // UBS
            ["JE00BRX98089"] = ("Financial Services", "Asset Management"),      // CVC Capital
            ["NL0012059018"] = ("Financial Services", "Asset Management"),      // EXOR
            ["NL0015073TS8"] = ("Financial Services", "Capital Markets"),       // CSG N.V.
            ["DE0005810055"] = ("Financial Services", "Financial Data & Exchanges"), // Deutsche Boerse
            ["NL0006294274"] = ("Financial Services", "Financial Data & Exchanges"), // Euronext

            // --- Health Care ---
            ["FR0000120578"] = ("Health Care", "Drug Manufacturers"),           // Sanofi
            ["CH0012032048"] = ("Health Care", "Drug Manufacturers"),           // Roche
            ["CH0012005267"] = ("Health Care", "Drug Manufacturers"),           // Novartis
            ["DE000BAY0017"] = ("Health Care", "Drug Manufacturers"),           // Bayer
            ["DE0006599905"] = ("Health Care", "Drug Manufacturers"),           // Merck KGaA
            ["BE0003739530"] = ("Health Care", "Drug Manufacturers"),           // UCB
            ["CH1335392721"] = ("Health Care", "Drug Manufacturers"),           // Galderma
            ["NL0010832176"] = ("Health Care", "Biotechnology"),                // argenx
            ["FR0000121667"] = ("Health Care", "Medical Devices"),              // EssilorLuxottica
            ["NL0000009538"] = ("Health Care", "Medical Devices"),              // Philips
            ["DE000SHL1006"] = ("Health Care", "Medical Devices"),              // Siemens Healthineers
            ["CH0432492467"] = ("Health Care", "Medical Devices"),              // Alcon
            ["DE0005785802"] = ("Health Care", "Medical Devices"),              // Fresenius Medical Care
            ["DE0007165631"] = ("Health Care", "Medical Devices"),              // Sartorius AG pref
            ["DE0005785604"] = ("Health Care", "Healthcare Plans"),             // Fresenius SE
            ["FR0014000MR3"] = ("Health Care", "Diagnostics & Research"),       // Eurofins Scientific

            // --- Consumer Cyclical ---
            ["FR0000121014"] = ("Consumer Cyclical", "Luxury Goods"),           // LVMH
            ["FR0000052292"] = ("Consumer Cyclical", "Luxury Goods"),           // Hermes
            ["FR0000121485"] = ("Consumer Cyclical", "Luxury Goods"),           // Kering
            ["CH0210483332"] = ("Consumer Cyclical", "Luxury Goods"),           // Richemont
            ["IT0004965148"] = ("Consumer Cyclical", "Luxury Goods"),           // Moncler
            ["DE0007100000"] = ("Consumer Cyclical", "Auto Manufacturers"),     // Mercedes-Benz
            ["DE0005190003"] = ("Consumer Cyclical", "Auto Manufacturers"),     // BMW
            ["DE0007664039"] = ("Consumer Cyclical", "Auto Manufacturers"),     // Volkswagen
            ["NL00150001Q9"] = ("Consumer Cyclical", "Auto Manufacturers"),     // Stellantis
            ["NL0011585146"] = ("Consumer Cyclical", "Auto Manufacturers"),     // Ferrari
            ["DE000PAG9113"] = ("Consumer Cyclical", "Auto Manufacturers"),     // Porsche
            ["FR0000131906"] = ("Consumer Cyclical", "Auto Manufacturers"),     // Renault
            ["FR001400AJ45"] = ("Consumer Cyclical", "Auto Parts"),             // Michelin
            ["DE0005439004"] = ("Consumer Cyclical", "Auto Parts"),             // Continental
            ["DE000A1EWWW0"] = ("Consumer Cyclical", "Footwear & Accessories"), // Adidas
            ["ES0148396007"] = ("Consumer Cyclical", "Apparel Retail"),         // Inditex
            ["DE000ZAL1111"] = ("Consumer Cyclical", "Internet Retail"),        // Zalando
            ["FR0000120404"] = ("Consumer Cyclical", "Hotels & Resorts"),       // Accor
            ["DE000TUAG505"] = ("Consumer Cyclical", "Travel Services"),        // TUI

            // --- Consumer Defensive ---
            ["CH0038863350"] = ("Consumer Defensive", "Packaged Foods"),        // Nestle
            ["FR0000120644"] = ("Consumer Defensive", "Packaged Foods"),        // Danone
            ["NL0015002MS2"] = ("Consumer Defensive", "Packaged Foods"),        // Magnum Ice Cream
            ["FR0000120321"] = ("Consumer Defensive", "Household & Personal Products"), // L'Oreal
            ["GB00BVZK7T90"] = ("Consumer Defensive", "Household & Personal Products"), // Unilever
            ["DE0006048432"] = ("Consumer Defensive", "Household & Personal Products"), // Henkel
            ["DE0005200000"] = ("Consumer Defensive", "Household & Personal Products"), // Beiersdorf
            ["BE0974293251"] = ("Consumer Defensive", "Beverages - Alcoholic"), // AB InBev
            ["NL0000009165"] = ("Consumer Defensive", "Beverages - Alcoholic"), // Heineken
            ["FR0000120693"] = ("Consumer Defensive", "Beverages - Alcoholic"), // Pernod Ricard
            ["NL0011794037"] = ("Consumer Defensive", "Grocery Stores"),        // Ahold Delhaize
            ["FR0000120172"] = ("Consumer Defensive", "Grocery Stores"),        // Carrefour

            // --- Basic Materials ---
            ["FR0000120073"] = ("Basic Materials", "Specialty Chemicals"),      // Air Liquide
            ["DE000BASF111"] = ("Basic Materials", "Specialty Chemicals"),      // BASF
            ["NL0013267909"] = ("Basic Materials", "Specialty Chemicals"),      // AKZO Nobel
            ["CH1216478797"] = ("Basic Materials", "Specialty Chemicals"),      // DSM Firmenich
            ["DE000SYM9999"] = ("Basic Materials", "Specialty Chemicals"),      // Symrise
            ["DE000EVNK013"] = ("Basic Materials", "Specialty Chemicals"),      // Evonik
            ["DE000A1DAHH0"] = ("Basic Materials", "Specialty Chemicals"),      // Brenntag
            ["NL0010801007"] = ("Basic Materials", "Specialty Chemicals"),      // IMCD
            ["DE000KSAG888"] = ("Basic Materials", "Agricultural Inputs"),      // K&S
            ["LU1598757687"] = ("Basic Materials", "Steel"),                    // ArcelorMittal
            ["DE0006047004"] = ("Basic Materials", "Building Materials"),       // Heidelberg Materials

            // --- Real Estate ---
            ["DE000A1ML7J1"] = ("Real Estate", "Real Estate Services"),         // Vonovia
            ["DE000LEG1110"] = ("Real Estate", "Real Estate Services"),         // LEG Immobilien
            ["FR0013326246"] = ("Real Estate", "REIT - Retail"),                // Unibail-Rodamco-Westfield
            ["FR0000121964"] = ("Real Estate", "REIT - Retail"),                // Klepierre

            // --- Alstom (Industrials, Railroads) ---
            ["FR0010220475"] = ("Industrials", "Railroads"),                    // Alstom
        };

    private static readonly Dictionary<string, string> CountryCodeToName =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Norden
            ["DK"] = "Denmark",
            ["FI"] = "Finland",
            ["NO"] = "Norway",
            ["SE"] = "Sweden",
            // North America
            ["CA"] = "Canada",
            ["US"] = "United States",
            // British Isles
            ["IE"] = "Ireland",
            ["GB"] = "United Kingdom",
            // Continental Europe
            ["BE"] = "Belgium",
            ["FR"] = "France",
            ["IT"] = "Italy",
            ["NL"] = "Netherlands",
            ["PT"] = "Portugal",
            ["ES"] = "Spain",
            ["CH"] = "Switzerland",
            ["DE"] = "Germany",
            ["AT"] = "Austria",
        };

    /// <summary>
    /// Infers a best-effort sector from the company name when the ISIN is not in
    /// <see cref="KnownSectors"/>. Returns null if no keyword matches.
    /// </summary>
    private static string? InferSectorFromName(string name)
    {
        var n = name.ToLowerInvariant();

        if (n.Contains("bank") || n.Contains("banco") || n.Contains("banque") ||
            n.Contains("credit") || n.Contains("crédit") || n.Contains("invest") ||
            n.Contains("holding") || n.Contains("capital") || n.Contains("finance") ||
            n.Contains("asset management") || n.Contains("insurance") ||
            n.Contains("assicura") || n.Contains("versicherung") || n.Contains("bourse"))
            return "Financial Services";

        if (n.Contains("pharma") || n.Contains("biotech") || n.Contains("therapeut") ||
            n.Contains("medical") || n.Contains("health") || n.Contains("hospital") ||
            n.Contains("diagnostics") || n.Contains("clinic") || n.Contains("laborator"))
            return "Health Care";

        if (n.Contains("semiconductor") || n.Contains("software") || n.Contains("digital") ||
            n.Contains("microelectron") || n.Contains("tech ") || n.Contains("technologies") ||
            n.Contains("technology") || n.Contains("systems") || n.Contains("data") ||
            n.Contains("internet") || n.Contains("cloud") || n.Contains("cyber"))
            return "Technology";

        if (n.Contains("telecom") || n.Contains("telephone") || n.Contains("mobile") ||
            n.Contains("broadcasting") || n.Contains("media") || n.Contains("entertainment"))
            return "Communication Services";

        if (n.Contains("oil") || n.Contains("gas") || n.Contains("petrol") || n.Contains("energy e&p"))
            return "Energy";

        if (n.Contains("electric") || n.Contains("power") || n.Contains("water") ||
            n.Contains("waste") || n.Contains("grid") || n.Contains("utility") || n.Contains("utilities"))
            return "Utilities";

        if (n.Contains("airline") || n.Contains("aviation") || n.Contains("aerospace") ||
            n.Contains("defense") || n.Contains("defence") || n.Contains("freight") ||
            n.Contains("logistic") || n.Contains("construc") || n.Contains("engineer") ||
            n.Contains("industrial") || n.Contains("industrie") || n.Contains("industries"))
            return "Industrials";

        if (n.Contains("luxury") || n.Contains("fashion") || n.Contains("auto") ||
            n.Contains("motor") || n.Contains("vehicle") || n.Contains("hotel") ||
            n.Contains("retail") || n.Contains("sport"))
            return "Consumer Cyclical";

        if (n.Contains("food") || n.Contains("beverage") || n.Contains("beer") ||
            n.Contains("grocery") || n.Contains("consumer") || n.Contains("cosmetic") ||
            n.Contains("personal care") || n.Contains("household"))
            return "Consumer Defensive";

        if (n.Contains("chemical") || n.Contains("chemical") || n.Contains("material") ||
            n.Contains("steel") || n.Contains("metal") || n.Contains("mining") ||
            n.Contains("mineral"))
            return "Basic Materials";

        if (n.Contains("real estate") || n.Contains("immobil") || n.Contains("property") ||
            n.Contains("reit") || n.Contains("realty"))
            return "Real Estate";

        return null;
    }

    /// <summary>
    /// Extracts the exchange code from the Nordnet BrokerSymbol slug.
    /// E.g. "asml-holding-asml-xams" → "XAMS".
    /// </summary>
    private static string MarketFromBrokerSymbol(string brokerSymbol)
    {
        var parts = brokerSymbol.Split('-');
        return parts.Length > 0 ? parts[^1].ToUpperInvariant() : "Europe";
    }

    public static async Task SeedAsync(AppDbContext db)
    {
        var csvRows = ReadCsv();

        // Load all existing assets that match any broker symbol from the CSV
        var brokerSymbols = csvRows.Select(r => r.BrokerSymbol).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingAssets = await db.Assets
            .Where(a => a.Broker == "NordNet" && a.BrokerSymbol != null && brokerSymbols.Contains(a.BrokerSymbol!))
            .ToListAsync();

        var existingByBrokerSymbol = existingAssets
            .Where(a => a.BrokerSymbol != null)
            .ToDictionary(a => a.BrokerSymbol!, StringComparer.OrdinalIgnoreCase);

        var toInsert = new List<Asset>();
        var insertedBrokerSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in csvRows)
        {
            var market   = MarketFromBrokerSymbol(row.BrokerSymbol);
            var country  = CountryCodeToName.TryGetValue(row.Country, out var cn) ? cn : row.Country;
            var region   = string.IsNullOrWhiteSpace(row.Region) ? "Europe" : row.Region;

            KnownSectors.TryGetValue(row.Isin, out var sectorInfo);
            var sector    = row.CsvSector    ?? sectorInfo.Sector    ?? InferSectorFromName(row.Name);
            var subsector = row.CsvSubsector ?? sectorInfo.Subsector ?? null;

            if (existingByBrokerSymbol.TryGetValue(row.BrokerSymbol, out var existing))
            {
                if (string.IsNullOrWhiteSpace(existing.Isin))    existing.Isin    = row.Isin;
                if (string.IsNullOrWhiteSpace(existing.Sector))   existing.Sector  = sector;
                if (string.IsNullOrWhiteSpace(existing.Subsector) && subsector != null)
                    existing.Subsector = subsector;
                if (string.IsNullOrWhiteSpace(existing.Country))  existing.Country = country;
                if (string.IsNullOrWhiteSpace(existing.Region))   existing.Region  = region;
                if (string.IsNullOrWhiteSpace(existing.Market))   existing.Market  = market;
                if (string.IsNullOrWhiteSpace(existing.IconUrl) && row.IconUrl != null) existing.IconUrl = row.IconUrl;
                if (string.IsNullOrWhiteSpace(existing.NnxId) && row.NnxId != null)     existing.NnxId    = row.NnxId;
                if (string.IsNullOrWhiteSpace(existing.WebsiteUrl) && row.WebsiteUrl != null)   existing.WebsiteUrl   = row.WebsiteUrl;
                if (string.IsNullOrWhiteSpace(existing.Description) && row.Description != null) existing.Description  = row.Description;
                if (string.IsNullOrWhiteSpace(existing.Ceo) && row.Ceo != null)                 existing.Ceo          = row.Ceo;
                if (string.IsNullOrWhiteSpace(existing.Address1) && row.Address1 != null)       existing.Address1     = row.Address1;
                if (string.IsNullOrWhiteSpace(existing.Address2) && row.Address2 != null)       existing.Address2     = row.Address2;
                if (existing.NumberShares == null && row.NumberShares != null)                  existing.NumberShares  = row.NumberShares;
                existing.Broker     = "NordNet";
                existing.Popularity = row.Popularity;
            }
            else if (insertedBrokerSymbols.Add(row.BrokerSymbol))
            {
                toInsert.Add(new Asset
                {
                    Id           = AppDbContext.AssetGuid(row.BrokerSymbol),
                    Name         = row.Name,
                    Type         = AssetType.Stock,
                    Symbol       = row.Symbol,
                    Market       = market,
                    Broker       = "NordNet",
                    BrokerSymbol = row.BrokerSymbol,
                    Isin         = row.Isin,
                    Sector       = sector,
                    Subsector    = subsector,
                    Country      = country,
                    Region       = region,
                    Popularity   = row.Popularity,
                    IconUrl      = row.IconUrl,
                    NnxId        = row.NnxId,
                    WebsiteUrl   = row.WebsiteUrl,
                    Description  = row.Description,
                    Ceo          = row.Ceo,
                    Address1     = row.Address1,
                    Address2     = row.Address2,
                    NumberShares = row.NumberShares,
                });
            }
        }

        if (toInsert.Count > 0)
            db.Assets.AddRange(toInsert);

        await db.SaveChangesAsync();
    }

    private record CsvRow(string BrokerSymbol, int Popularity, string Name, string Isin, string Symbol, string Country, string Region, string? NnxId, string? IconUrl, string? WebsiteUrl, string? Description, string? Ceo, string? Address1, string? Address2, long? NumberShares, string? CsvSector, string? CsvSubsector);

    private static List<CsvRow> ReadCsv()
    {
        var assembly   = Assembly.GetExecutingAssembly();
        const string resourceName = "StocksPlatform.Services.Seeding.Data.nordnet_all_stocks.csv";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new System.IO.StreamReader(stream);

        var rows = new List<CsvRow>();
        var header = reader.ReadLine(); // skip header

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var cols = SplitCsvLine(line);
            if (cols.Length < 6) continue;

            // BrokerSymbol,Popularity,Name,ISIN,Symbol,Country,NnxId,NordnetId,Icon,Region
            var brokerSymbol = cols[0].Trim();
            int.TryParse(cols[1].Trim(), out var popularity);
            var name         = cols[2].Trim();
            var isin         = cols[3].Trim();
            var symbol       = cols[4].Trim();
            var country      = cols[5].Trim();
            var nnxId        = cols.Length >= 7  ? NullIfEmpty(cols[6]) : null;
            var iconUrl      = cols.Length >= 9  ? cols[8].Trim()  : null;
            var region       = cols.Length >= 10 ? cols[9].Trim()  : "Europe";
            var websiteUrl   = cols.Length >= 11 ? NullIfEmpty(cols[10]) : null;
            var description  = cols.Length >= 12 ? NullIfEmpty(cols[11]) : null;
            var ceo          = cols.Length >= 13 ? NullIfEmpty(cols[12]) : null;
            var address1     = cols.Length >= 14 ? NullIfEmpty(cols[13]) : null;
            var address2     = cols.Length >= 15 ? NullIfEmpty(cols[14]) : null;
            long? numberShares = cols.Length >= 16 && long.TryParse(cols[15].Trim(), out var ns) ? ns : null;
            var csvSector    = cols.Length >= 17 ? NullIfEmpty(cols[16]) : null;
            var csvSubsector = cols.Length >= 18 ? NullIfEmpty(cols[17]) : null;

            if (string.IsNullOrWhiteSpace(brokerSymbol) || string.IsNullOrWhiteSpace(isin))
                continue;

            rows.Add(new CsvRow(brokerSymbol, popularity, name, isin, symbol, country,
                string.IsNullOrWhiteSpace(region) ? "Europe" : region,
                nnxId,
                string.IsNullOrWhiteSpace(iconUrl) ? null : iconUrl,
                websiteUrl, description, ceo, address1, address2, numberShares, csvSector, csvSubsector));
        }

        return rows;
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Splits a CSV line respecting double-quoted fields.</summary>
    private static string[] SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new System.Text.StringBuilder();
        bool inQuote = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuote && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuote = !inQuote;
                }
            }
            else if (c == ',' && !inQuote)
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields.ToArray();
    }
}
