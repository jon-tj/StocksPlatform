using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;
using StocksPlatform.Models;

namespace StocksPlatform.Services.Seeding;

/// <summary>
/// Idempotently seeds all known OSE-listed tickers into the Assets table.
/// Skips any symbol that already exists. Safe to run on every startup.
/// </summary>
public static class OseTickerSeeder
{
    // (Symbol, Display name, ISIN)
    private static readonly (string Symbol, string Name, string? Isin)[] Tickers =
    [
        ("2020",   "2020 Bulkers",                       "BMG9156K1018"),
        ("ABG",    "ABG Sundal Collier Holding",          "NO0003021909"),
        ("ABL",    "ABL Group ASA",                       "NO0010715394"),
        ("ACR",    "Axactor ASA",                         "NO0010840515"),
        ("AFG",    "AF Gruppen",                          "NO0003078107"),
        ("AFK",    "Arendals Fossekompani",               "NO0003572802"),
        ("AGLX",   "Agilyx ASA",                          "NO0010872468"),
        ("AKAST",  "Akastor",                             "NO0010215684"),
        ("AKBM",   "Aker BioMarine ASA",                  "NO0010886625"),
        ("AKER",   "Aker",                                "NO0010234552"),
        ("AKH",    "Aker Horizons ASA",                   "NO0010921232"),
        ("AKRBP",  "Aker BP",                             "NO0010345853"),
        ("AKSO",   "Aker Solutions",                      "NO0010716582"),
        ("AKVA",   "AKVA Group",                          "NO0003097503"),
        ("APR",    "Appear ASA",                          "NO0013683821"),
        ("ARCH",   "Archer",                              "BMG0451H2087"),
        ("ARR",    "Arribatec Group ASA",                 "NO0013682948"),
        ("ASA",    "Atlantic Sapphire",                   "NO0013340802"),
        ("ATEA",   "Atea",                                "NO0004822503"),
        ("AURG",   "Aurskog Sparebank",                   "NO0006001601"),
        ("AUSS",   "Austevoll Seafood",                   "NO0010073489"),
        ("AUTO",   "AutoStore Holdings Ltd.",             "BMG0670A1099"),
        ("AZT",    "ArcticZymes Technologies",            "NO0010014632"),
        ("B2I",    "B2 Impact ASA",                       "NO0010633951"),
        ("BAKKA",  "Bakkafrost",                          "FO0000000179"),
        ("BEWI",   "Bewi",                                "NO0010890965"),
        ("BIEN",   "Bien Sparebank ASA",                  "NO0012706763"),
        ("BNOR",   "Bluenord ASA",                        "NO0010379266"),
        ("BONHR",  "Bonheur",                             "NO0003110603"),
        ("BOR",    "Borgestad",                           "NO0013256180"),
        ("BOUV",   "Bouvet",                              "NO0010360266"),
        ("BRG",    "Borregaard",                          "NO0010657505"),
        ("BWE",    "BW Energy Limited",                   "BMG0702P1086"),
        ("BWLPG",  "BW LPG",                             "SGXZ69436764"),
        ("BWO",    "BW Offshore Limited",                 "BMG1738J1247"),
        ("CADLR",  "Cadeler A/S",                         "DK0061412772"),
        ("CAPSL",  "Capsol Technologies ASA",             "NO0010923121"),
        ("CAVEN",  "Cavendish Hydrogen ASA",              "NO0013219535"),
        ("CLOUD",  "Cloudberry Clean Energy ASA",         "NO0010876642"),
        ("CMBTO",  "CMB.TECH NV",                         "BE0003816338"),
        ("CONTX",  "ContextVision",                       "SE0014731154"),
        ("CRNA",   "Circio Holding ASA",                  "NO0013033795"),
        ("DELIA",  "Dellia Group",                        "NO0012697095"),
        ("DFENS",  "Fjord Defence Group ASA",             "NO0013647693"),
        ("DNB",    "DNB Bank ASA",                        "NO0010161896"),
        ("DNO",    "DNO",                                 "NO0003921009"),
        ("DOFG",   "DOF Group ASA",                       "NO0012851874"),
        ("EIOF",   "Eidesvik Offshore",                   "NO0010263023"),
        ("ELABS",  "Elliptic Laboratories ASA",           "NO0010722283"),
        ("ELK",    "Elkem",                               "NO0010816093"),
        ("ELMRA",  "Elmera Group ASA",                    "NO0010815673"),
        ("ELO",    "Elopak ASA",                          "NO0011002586"),
        ("EMGS",   "Electromagnetic GeoServices",         "NO0010358484"),
        ("ENDUR",  "Endúr",                               "NO0012555459"),
        ("ENH",    "SED Energy Holdings PLC",             "CY0101162119"),
        ("ENSU",   "Ensurge Micropower ASA",              "NO0013186460"),
        ("ENTRA",  "Entra",                               "NO0010716418"),
        ("ENVIP",  "Envipco Holding N.V.",                "NL0015000GX8"),
        ("EPR",    "Europris",                            "NO0010735343"),
        ("EQNR",   "Equinor",                             "NO0010096985"),
        ("EQVA",   "EQVA ASA",                            "NO0010708605"),
        ("FRO",    "Frontline PLC",                       "CY0200352116"),
        ("GENT",   "Gentian Diagnostics ASA",             "NO0010748866"),
        ("GJF",    "Gjensidige Forsikring",               "NO0010582521"),
        ("GOD",    "Goodtech",                            "NO0004913609"),
        ("GSF",    "Grieg Seafood",                       "NO0010365521"),
        ("GYL",    "Gyldendal",                           "NO0004288200"),
        ("HAFNI",  "Hafnia Limited",                      "SGXZ53070850"),
        ("HAUTO",  "Høegh Autoliners ASA",                "NO0011082075"),
        ("HAVI",   "Havila Shipping",                     "NO0010257728"),
        ("HBC",    "Hofseth BioCare",                     "NO0010598683"),
        ("HELG",   "SpareBank 1 Helgeland",               "NO0010029804"),
        ("HERMA",  "Hermana Holding ASA",                 "NO0013401380"),
        ("HEX",    "Hexagon Composites",                  "NO0003067902"),
        ("HGSB",   "Haugesund Sparebank",                 "NO0010764053"),
        ("HPUR",   "Hexagon Purus ASA",                   "NO0010904923"),
        ("HSHP",   "Himalaya Shipping Ltd",               "BMG4660A1036"),
        ("HSPG",   "Høland og Setskog Sparebank",         "NO0010012636"),
        ("HYPRO",  "HydrogenPro ASA",                     "NO0010892359"),
        ("IDEX",   "IDEX Biometrics",                     "NO0013536078"),
        ("IOX",    "InterOil Exploration and Production", "NO0013119255"),
        ("ITERA",  "Itera",                               "NO0010001118"),
        ("IWS",    "Integrated Wind Solutions ASA",       "NO0013461350"),
        ("JAREN",  "Jæren Sparebank",                     "NO0010359433"),
        ("JIN",    "Jinhui Shipping and Transportation",  "BMG5137R1088"),
        ("KCC",    "Klaveness Combination Carriers",      "NO0010833262"),
        ("KID",    "Kid",                                 "NO0010743545"),
        ("KIT",    "Kitron",                              "NO0003079709"),
        ("KMCP",   "KMC Properties ASA",                  "NO0013711721"),
        ("KOA",    "Kongsberg Automotive",                "NO0003033102"),
        ("KOG",    "Kongsberg Gruppen",                   "NO0013536151"),
        ("KOMPL",  "Komplett ASA",                        "NO0011016040"),
        ("LIFE",   "Lifecare ASA",                        "NO0013355859"),
        ("LINK",   "Link Mobility Group Holding",         "NO0010894231"),
        ("LSG",    "Lerøy Seafood Group",                 "NO0003096208"),
        ("MEDI",   "Medistim",                            "NO0010159684"),
        ("MELG",   "Melhus Sparebank",                    "NO0006001908"),
        ("MGN",    "Magnora",                             "NO0010187032"),
        ("MING",   "SpareBank 1 SMN",                     "NO0006390301"),
        ("MORG",   "Sparebanken Møre",                    "NO0012483207"),
        ("MORLD",  "Moreld ASA",                          "NO0013325506"),
        ("MOWI",   "Mowi",                                "NO0003054108"),
        ("MPCC",   "MPC Container Ships",                 "NO0010791353"),
        ("MULTI",  "Multiconsult",                        "NO0010734338"),
        ("NAPA",   "Napatech",                            "DK0060520450"),
        ("NAS",    "Norwegian Air Shuttle",               "NO0010196140"),
        ("NAVA",   "Navamedic",                           "NO0010205966"),
        ("NEL",    "Nel",                                 "NO0010081235"),
        ("NEXT",   "Next Biometrics Group",               "NO0010629108"),
        ("NHY",    "Norsk Hydro",                         "NO0005052605"),
        ("NKR",    "Nekkar",                              "NO0003049405"),
        ("NOD",    "Nordic Semiconductor",                "NO0003055501"),
        ("NOL",    "Northern Ocean Ltd.",                 "BMG6682J1036"),
        ("NOM",    "Nordic Mining",                       "NO0013162693"),
        ("NONG",   "SpareBank 1 Nord-Norge",              "NO0006000801"),
        ("NORBT",  "Norbit",                              "NO0010856511"),
        ("NORCO",  "Norconsult ASA",                      "NO0013052209"),
        ("NRC",    "NRC Group",                           "NO0003679102"),
        ("NSKOG",  "Norske Skog",                         "NO0010861115"),
        ("NYKD",   "Nykode Therapeutics ASA",             "NO0010714785"),
        ("ODF",    "Odfjell Ser. A",                      "NO0003399909"),
        ("ODFB",   "Odfjell Ser. B",                      "NO0003399917"),
        ("ODL",    "Odfjell Drilling",                    "BMG671801022"),
        ("OET",    "Okeanis Eco Tankers",                 "MHY641771016"),
        ("OKEA",   "Okea",                                "NO0010816895"),
        ("ONCIN",  "Oncoinvent ASA",                      "NO0013711713"),
        ("ORK",    "Orkla",                               "NO0003733800"),
        ("OTEC",   "Otello Corporation",                  "NO0010040611"),
        ("OTL",    "Odfjell Technology Ltd",              "BMG6716L1081"),
        ("OTOVO",  "Otovo ASA",                           "NO0013721613"),
        ("PARB",   "Pareto Bank",                         "NO0010397581"),
        ("PCIB",   "PCI Biotech Holding",                 "NO0010405640"),
        ("PEN",    "Panoro Energy",                       "NO0010564701"),
        ("PEXIP",  "Pexip Holding",                       "NO0010840507"),
        ("PHO",    "Photocure",                           "NO0010000045"),
        ("PLSV",   "Paratus Energy Services Ltd.",        "BMG6904D1083"),
        ("PLT",    "poLight",                             "NO0012535832"),
        ("PNOR",   "Petronor E&P ASA",                    "NO0012942525"),
        ("POL",    "Polaris Media",                       "NO0010466022"),
        ("PROT",   "Protector Forsikring",                "NO0010209331"),
        ("PRS",    "Prosafe",                             "NO0010861990"),
        ("PSE",    "Petrolia",                            "CY0102630916"),
        ("PUBLI",  "Public Property Invest ASA",          "NO0013178616"),
        ("QEC",    "Questerre Energy Corporation",        "CA74836K1003"),
        ("RANA",   "Rana Gruber ASA",                     "NO0010907389"),
        ("REACH",  "Reach Subsea",                        "NO0003117202"),
        ("RECSI",  "REC Silicon",                         "NO0010112675"),
        ("RING",   "SpareBank 1 Ringerike Hadeland",      "NO0006390400"),
        ("ROGS",   "Rogaland Sparebank",                  "NO0006001007"),
        ("SAGA",   "Saga Pure",                           "NO0010572589"),
        ("SALM",   "SalMar",                              "NO0010310956"),
        ("SALME",  "Salmon Evolution ASA",                "NO0010892094"),
        ("SATS",   "Sats",                                "NO0010863285"),
        ("SB1NO",  "SpareBank 1 Sør-Norge ASA",           "NO0010631567"),
        ("SBNOR",  "Sparebanken Norge",                   "NO0006000900"),
        ("SBO",    "Selvaag Bolig",                       "NO0010612450"),
        ("SCANA",  "Scana",                               "NO0003053308"),
        ("SCATC",  "Scatec ASA",                          "NO0010715139"),
        ("SDSD",   "S.D. Standard ETC PLC",               "CY0101550917"),
        ("SKUE",   "Skue Sparebank",                      "NO0006001809"),
        ("SMCRT",  "SmartCraft ASA",                      "NO0011008971"),
        ("SMOP",   "Smartoptics Group ASA",               "NO0011012502"),
        ("SNI",    "Stolt-Nielsen",                       "BMG850801025"),
        ("SNOR",   "SpareBank 1 Nordmøre",                "NO0010691660"),
        ("SNTIA",  "Sentia ASA",                          "NO0013573014"),
        ("SOAG",   "SpareBank 1 Østfold Akershus",        "NO0010285562"),
        ("SOFF",   "Solstad Offshore",                    "NO0003080608"),
        ("SOGN",   "Sogn Sparebank",                      "NO0006000603"),
        ("SOMA",   "Solstad Maritime ASA",                "NO0013135368"),
        ("SPIR",   "Spir Group ASA",                      "NO0012548819"),
        ("SPOG",   "Sparebanken Øst",                     "NO0006222009"),
        ("SPOL",   "SpareBank 1 Østlandet",               "NO0010751910"),
        ("STB",    "Storebrand",                          "NO0003053605"),
        ("STRO",   "StrongPoint",                         "NO0010098247"),
        ("SUBC",   "Subsea 7",                            "LU0075646355"),
        ("SWON",   "SoftwareOne Holding",                 "CH0496451508"),
        ("TECH",   "Techstep",                            "NO0012916131"),
        ("TEKNA",  "Tekna Holding ASA",                   "NO0010951577"),
        ("TEL",    "Telenor",                             "NO0010063308"),
        ("TGS",    "TGS ASA",                             "NO0003078800"),
        ("TIETO",  "TietoEVRY",                           "FI0009000277"),
        ("TOM",    "Tomra Systems",                       "NO0012470089"),
        ("TRMED",  "Thor Medical ASA",                    "NO0010597883"),
        ("TRSB",   "Trøndelag Sparebank",                 "NO0010788268"),
        ("VAR",    "Vår Energi ASA",                      "NO0011202772"),
        ("VEI",    "Veidekke",                            "NO0005806802"),
        ("VEND",   "Vend Marketplaces ASA",               "NO0010736879"),
        ("VISTN",  "Vistin Pharma",                       "NO0010734122"),
        ("VOW",    "VOW",                                 "NO0010708068"),
        ("VVL",    "Voss Veksel- og Landmandsbank",       "NO0003025009"),
        ("WAWI",   "Wallenius Wilhelmsen",                "NO0010571680"),
        ("WSTEP",  "Webstep",                             "NO0010609662"),
        ("WWI",    "Wilh. Wilhelmsen Holding Ser. A",     "NO0010571698"),
        ("WWIB",   "Wilh. Wilhelmsen Holding Ser. B",     "NO0010576010"),
        ("YAR",    "Yara International",                  "NO0010208051"),
        ("ZAL",    "Zalaris",                             "NO0010708910"),
        ("ZAP",    "Zaptec ASA",                          "NO0010713936"),
        ("ZLNA",   "Zelluna",                             "NO0013524942"),
    ];

    private static readonly Dictionary<string, (string Sector, string Subsector)> SectorOverrides =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["EQNR"] = ("Energy", "Integrated Oil & Gas"),
            ["AKRBP"] = ("Energy", "Oil & Gas E&P"),
            ["VAR"] = ("Energy", "Oil & Gas E&P"),
            ["OKEA"] = ("Energy", "Oil & Gas E&P"),
            ["PEN"] = ("Energy", "Oil & Gas E&P"),
            ["DNO"] = ("Energy", "Oil & Gas E&P"),
            ["TGS"] = ("Energy", "Oil & Gas Services"),
            ["SUBC"] = ("Energy", "Oil & Gas Equipment & Services"),
            ["DNB"] = ("Financial Services", "Banks"),
            ["GJF"] = ("Financial Services", "Insurance"),
            ["STB"] = ("Financial Services", "Asset Management"),
            ["NHY"] = ("Basic Materials", "Aluminum"),
            ["YAR"] = ("Basic Materials", "Agricultural Inputs"),
            ["TEL"] = ("Communication Services", "Telecom Services"),
            ["KOG"] = ("Industrials", "Aerospace & Defense"),
            ["NEL"] = ("Industrials", "Electrical Equipment"),
            ["MOWI"] = ("Consumer Defensive", "Packaged Foods"),
            ["BAKKA"] = ("Consumer Defensive", "Packaged Foods"),
            ["LSG"] = ("Consumer Defensive", "Packaged Foods"),
            ["SALM"] = ("Consumer Defensive", "Packaged Foods"),
            ["SCATC"] = ("Utilities", "Renewable Utilities"),
            ["NOD"] = ("Technology", "Semiconductors"),
            ["KIT"] = ("Technology", "Electronic Components"),
            ["PHO"] = ("Health Care", "Medical Devices"),
            ["AZT"] = ("Health Care", "Biotechnology"),
            ["NYKD"] = ("Health Care", "Biotechnology"),
        };

    // Derives the NordNet URL slug for an OSE asset.
    // Pattern: {name-as-slug}-{symbol-lower}-xosl
    // Examples: "Equinor" + EQNR → equinor-eqnr-xosl
    //           "Aker BP" + AKRBP → aker-bp-akrbp-xosl
    private static string NordnetSlug(string name, string symbol)
    {
        var s = name.ToLowerInvariant()
            .Replace("/", "-")
            .Replace(".", "")
            .Replace(",", "")
            .Replace("'", "")
            .Replace("(", "")
            .Replace(")", "")
            .Replace(" ", "-");
        s = System.Text.RegularExpressions.Regex.Replace(s, "-{2,}", "-").Trim('-');
        return $"{s}-{symbol.ToLowerInvariant()}-xosl";
    }

    public static async Task SeedAsync(AppDbContext db)
    {
        var existingAssets = await db.Assets
            .Where(a => a.Market == "OSE")
            .ToListAsync();

        var existingBySymbol = existingAssets
            .Where(a => a.Symbol != null)
            .ToDictionary(a => a.Symbol!);

        var toInsert = new List<Asset>();

        foreach (var t in Tickers)
        {
            var (country, region) = InferCountryRegion(t.Isin);
            var (sector, subsector) = InferSectorAndSubsector(t.Symbol, t.Name);

            if (existingBySymbol.TryGetValue(t.Symbol, out var existing))
            {
                if (existing.Isin == null && t.Isin != null)
                    existing.Isin = t.Isin;

                if (string.IsNullOrWhiteSpace(existing.Country))
                    existing.Country = country;

                if (string.IsNullOrWhiteSpace(existing.Region))
                    existing.Region = region;

                if (string.IsNullOrWhiteSpace(existing.Sector) && sector is not null)
                    existing.Sector = sector;

                if (string.IsNullOrWhiteSpace(existing.Subsector) && subsector is not null)
                    existing.Subsector = subsector;

                if (string.IsNullOrWhiteSpace(existing.BrokerSymbol))
                    existing.BrokerSymbol = NordnetSlug(t.Name, t.Symbol);
            }
            else
            {
                toInsert.Add(new Asset
                {
                    Id           = AppDbContext.AssetGuid(t.Symbol),
                    Name         = t.Name,
                    Type         = AssetType.Stock,
                    Symbol       = t.Symbol,
                    Market       = "OSE",
                    Broker       = "NordNet",
                    BrokerSymbol = NordnetSlug(t.Name, t.Symbol),
                    Isin         = t.Isin,
                    Sector       = sector,
                    Subsector    = subsector,
                    Country      = country,
                    Region       = region,
                });
            }
        }

        if (toInsert.Count > 0)
            db.Assets.AddRange(toInsert);

        await db.SaveChangesAsync();
    }

    private static (string Country, string Region) InferCountryRegion(string? isin)
    {
        if (string.IsNullOrWhiteSpace(isin) || isin.Length < 2)
            return ("Norway", "Europe");

        return isin[..2].ToUpperInvariant() switch
        {
            "NO" => ("Norway", "Europe"),
            "SE" => ("Sweden", "Europe"),
            "DK" => ("Denmark", "Europe"),
            "FI" => ("Finland", "Europe"),
            "FO" => ("Faroe Islands", "Europe"),
            "NL" => ("Netherlands", "Europe"),
            "LU" => ("Luxembourg", "Europe"),
            "CH" => ("Switzerland", "Europe"),
            "CY" => ("Cyprus", "Europe"),
            "CA" => ("Canada", "North America"),
            "US" => ("United States", "North America"),
            "IE" => ("Ireland", "Europe"),
            "BM" => ("Bermuda", "North America"),
            "MH" => ("Marshall Islands", "Oceania"),
            "GB" => ("United Kingdom", "Europe"),
            _ => ("Norway", "Europe"),
        };
    }

    private static (string? Sector, string? Subsector) InferSectorAndSubsector(string symbol, string name)
    {
        if (SectorOverrides.TryGetValue(symbol, out var manual))
            return manual;

        var n = name.ToUpperInvariant();

        if (n.Contains("SPAREBANK") || n.Contains(" BANK") || n.EndsWith("BANK"))
            return ("Financial Services", "Banks");

        if (n.Contains("FORSIKRING") || n.Contains("INSURANCE"))
            return ("Financial Services", "Insurance");

        if (n.Contains("SEAFOOD") || n.Contains("SALMON") || n.Contains("FISH"))
            return ("Consumer Defensive", "Packaged Foods");

        if (n.Contains("SHIPPING") || n.Contains("TANKER") || n.Contains("CONTAINER") || n.Contains("AUTOLINERS"))
            return ("Industrials", "Marine Transportation");

        if (n.Contains("DRILLING") || n.Contains("OFFSHORE") || n.Contains("PETROL") || n.Contains("ENERGY"))
            return ("Energy", "Oil & Gas");

        if (n.Contains("BIO") || n.Contains("PHARMA") || n.Contains("THERAPEUTIC") || n.Contains("MEDI") || n.Contains("HEALTH"))
            return ("Health Care", "Biotechnology");

        if (n.Contains("TECH") || n.Contains("SOFTWARE") || n.Contains("SEMICONDUCTOR") || n.Contains("BIOMETRICS"))
            return ("Technology", "Software");

        if (n.Contains("PROPERTY") || n.Contains("BOLIG") || n.Contains("REAL ESTATE"))
            return ("Real Estate", "Real Estate Services");

        if (n.Contains("TELECOM") || n.Contains("TELENOR"))
            return ("Communication Services", "Telecom Services");

        if (n.Contains("HYDRO") || n.Contains("COMPOSITES") || n.Contains("CHEMICAL"))
            return ("Basic Materials", "Specialty Chemicals");

        if (n.Contains("POWER") || n.Contains("RENEW") || n.Contains("CLEAN"))
            return ("Utilities", "Renewable Utilities");

        return (null, null);
    }
}
