using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;
using StocksPlatform.Models;

namespace StocksPlatform.Services;

/// <summary>
/// Idempotently seeds all known OSE-listed tickers into the Assets table.
/// Skips any symbol that already exists. Safe to run on every startup.
/// </summary>
public static class OseTickerSeeder
{
    // (Symbol, Display name)
    private static readonly (string Symbol, string Name)[] Tickers =
    [
        ("2020",   "2020 Bulkers"),
        ("ABG",    "ABG Sundal Collier Holding"),
        ("ABL",    "ABL Group ASA"),
        ("ACR",    "Axactor ASA"),
        ("AFG",    "AF Gruppen"),
        ("AFK",    "Arendals Fossekompani"),
        ("AGLX",   "Agilyx ASA"),
        ("AKAST",  "Akastor"),
        ("AKBM",   "Aker BioMarine ASA"),
        ("AKER",   "Aker"),
        ("AKH",    "Aker Horizons ASA"),
        ("AKRBP",  "Aker BP"),
        ("AKSO",   "Aker Solutions"),
        ("AKVA",   "AKVA Group"),
        ("APR",    "Appear ASA"),
        ("ARCH",   "Archer"),
        ("ARR",    "Arribatec Group ASA"),
        ("ASA",    "Atlantic Sapphire"),
        ("ATEA",   "Atea"),
        ("AURG",   "Aurskog Sparebank"),
        ("AUSS",   "Austevoll Seafood"),
        ("AUTO",   "AutoStore Holdings Ltd."),
        ("AZT",    "ArcticZymes Technologies"),
        ("B2I",    "B2 Impact ASA"),
        ("BAKKA",  "Bakkafrost"),
        ("BEWI",   "Bewi"),
        ("BIEN",   "Bien Sparebank ASA"),
        ("BNOR",   "Bluenord ASA"),
        ("BONHR",  "Bonheur"),
        ("BOR",    "Borgestad"),
        ("BOUV",   "Bouvet"),
        ("BRG",    "Borregaard"),
        ("BWE",    "BW Energy Limited"),
        ("BWLPG",  "BW LPG"),
        ("BWO",    "BW Offshore Limited"),
        ("CADLR",  "Cadeler A/S"),
        ("CAPSL",  "Capsol Technologies ASA"),
        ("CAVEN",  "Cavendish Hydrogen ASA"),
        ("CLOUD",  "Cloudberry Clean Energy ASA"),
        ("CMBTO",  "CMB.TECH NV"),
        ("CONTX",  "ContextVision"),
        ("CRNA",   "Circio Holding ASA"),
        ("DELIA",  "Dellia Group"),
        ("DFENS",  "Fjord Defence Group ASA"),
        ("DNB",    "DNB Bank ASA"),
        ("DNO",    "DNO"),
        ("DOFG",   "DOF Group ASA"),
        ("EIOF",   "Eidesvik Offshore"),
        ("ELABS",  "Elliptic Laboratories ASA"),
        ("ELK",    "Elkem"),
        ("ELMRA",  "Elmera Group ASA"),
        ("ELO",    "Elopak ASA"),
        ("EMGS",   "Electromagnetic GeoServices"),
        ("ENDUR",  "Endúr"),
        ("ENH",    "SED Energy Holdings PLC"),
        ("ENSU",   "Ensurge Micropower ASA"),
        ("ENTRA",  "Entra"),
        ("ENVIP",  "Envipco Holding N.V."),
        ("EPR",    "Europris"),
        ("EQNR",   "Equinor"),
        ("EQVA",   "EQVA ASA"),
        ("FRO",    "Frontline PLC"),
        ("GENT",   "Gentian Diagnostics ASA"),
        ("GJF",    "Gjensidige Forsikring"),
        ("GOD",    "Goodtech"),
        ("GSF",    "Grieg Seafood"),
        ("GYL",    "Gyldendal"),
        ("HAFNI",  "Hafnia Limited"),
        ("HAUTO",  "Høegh Autoliners ASA"),
        ("HAVI",   "Havila Shipping"),
        ("HBC",    "Hofseth BioCare"),
        ("HELG",   "SpareBank 1 Helgeland"),
        ("HERMA",  "Hermana Holding ASA"),
        ("HEX",    "Hexagon Composites"),
        ("HGSB",   "Haugesund Sparebank"),
        ("HPUR",   "Hexagon Purus ASA"),
        ("HSHP",   "Himalaya Shipping Ltd"),
        ("HSPG",   "Høland og Setskog Sparebank"),
        ("HYPRO",  "HydrogenPro ASA"),
        ("IDEX",   "IDEX Biometrics"),
        ("IOX",    "InterOil Exploration and Production"),
        ("ITERA",  "Itera"),
        ("IWS",    "Integrated Wind Solutions ASA"),
        ("JAREN",  "Jæren Sparebank"),
        ("JIN",    "Jinhui Shipping and Transportation"),
        ("KCC",    "Klaveness Combination Carriers"),
        ("KID",    "Kid"),
        ("KIT",    "Kitron"),
        ("KMCP",   "KMC Properties ASA"),
        ("KOA",    "Kongsberg Automotive"),
        ("KOG",    "Kongsberg Gruppen"),
        ("KOMPL",  "Komplett ASA"),
        ("LIFE",   "Lifecare ASA"),
        ("LINK",   "Link Mobility Group Holding"),
        ("LSG",    "Lerøy Seafood Group"),
        ("MEDI",   "Medistim"),
        ("MELG",   "Melhus Sparebank"),
        ("MGN",    "Magnora"),
        ("MING",   "SpareBank 1 SMN"),
        ("MORG",   "Sparebanken Møre"),
        ("MORLD",  "Moreld ASA"),
        ("MOWI",   "Mowi"),
        ("MPCC",   "MPC Container Ships"),
        ("MULTI",  "Multiconsult"),
        ("NAPA",   "Napatech"),
        ("NAS",    "Norwegian Air Shuttle"),
        ("NAVA",   "Navamedic"),
        ("NEL",    "Nel"),
        ("NEXT",   "Next Biometrics Group"),
        ("NHY",    "Norsk Hydro"),
        ("NKR",    "Nekkar"),
        ("NOD",    "Nordic Semiconductor"),
        ("NOL",    "Northern Ocean Ltd."),
        ("NOM",    "Nordic Mining"),
        ("NONG",   "SpareBank 1 Nord-Norge"),
        ("NORBT",  "Norbit"),
        ("NORCO",  "Norconsult ASA"),
        ("NRC",    "NRC Group"),
        ("NSKOG",  "Norske Skog"),
        ("NYKD",   "Nykode Therapeutics ASA"),
        ("ODF",    "Odfjell Ser. A"),
        ("ODFB",   "Odfjell Ser. B"),
        ("ODL",    "Odfjell Drilling"),
        ("OET",    "Okeanis Eco Tankers"),
        ("OKEA",   "Okea"),
        ("ONCIN",  "Oncoinvent ASA"),
        ("ORK",    "Orkla"),
        ("OTEC",   "Otello Corporation"),
        ("OTL",    "Odfjell Technology Ltd"),
        ("OTOVO",  "Otovo ASA"),
        ("PARB",   "Pareto Bank"),
        ("PCIB",   "PCI Biotech Holding"),
        ("PEN",    "Panoro Energy"),
        ("PEXIP",  "Pexip Holding"),
        ("PHO",    "Photocure"),
        ("PLSV",   "Paratus Energy Services Ltd."),
        ("PLT",    "poLight"),
        ("PNOR",   "Petronor E&P ASA"),
        ("POL",    "Polaris Media"),
        ("PROT",   "Protector Forsikring"),
        ("PRS",    "Prosafe"),
        ("PSE",    "Petrolia"),
        ("PUBLI",  "Public Property Invest ASA"),
        ("QEC",    "Questerre Energy Corporation"),
        ("RANA",   "Rana Gruber ASA"),
        ("REACH",  "Reach Subsea"),
        ("RECSI",  "REC Silicon"),
        ("RING",   "SpareBank 1 Ringerike Hadeland"),
        ("ROGS",   "Rogaland Sparebank"),
        ("SAGA",   "Saga Pure"),
        ("SALM",   "SalMar"),
        ("SALME",  "Salmon Evolution ASA"),
        ("SATS",   "Sats"),
        ("SB1NO",  "SpareBank 1 Sør-Norge ASA"),
        ("SBNOR",  "Sparebanken Norge"),
        ("SBO",    "Selvaag Bolig"),
        ("SCANA",  "Scana"),
        ("SCATC",  "Scatec ASA"),
        ("SDSD",   "S.D. Standard ETC PLC"),
        ("SKUE",   "Skue Sparebank"),
        ("SMCRT",  "SmartCraft ASA"),
        ("SMOP",   "Smartoptics Group ASA"),
        ("SNI",    "Stolt-Nielsen"),
        ("SNOR",   "SpareBank 1 Nordmøre"),
        ("SNTIA",  "Sentia ASA"),
        ("SOAG",   "SpareBank 1 Østfold Akershus"),
        ("SOFF",   "Solstad Offshore"),
        ("SOGN",   "Sogn Sparebank"),
        ("SOMA",   "Solstad Maritime ASA"),
        ("SPIR",   "Spir Group ASA"),
        ("SPOG",   "Sparebanken Øst"),
        ("SPOL",   "SpareBank 1 Østlandet"),
        ("STB",    "Storebrand"),
        ("STRO",   "StrongPoint"),
        ("SUBC",   "Subsea 7"),
        ("SWON",   "SoftwareOne Holding"),
        ("TECH",   "Techstep"),
        ("TEKNA",  "Tekna Holding ASA"),
        ("TEL",    "Telenor"),
        ("TGS",    "TGS ASA"),
        ("TIETO",  "TietoEVRY"),
        ("TOM",    "Tomra Systems"),
        ("TRMED",  "Thor Medical ASA"),
        ("TRSB",   "Trøndelag Sparebank"),
        ("VAR",    "Vår Energi ASA"),
        ("VEI",    "Veidekke"),
        ("VEND",   "Vend Marketplaces ASA"),
        ("VISTN",  "Vistin Pharma"),
        ("VOW",    "VOW"),
        ("VVL",    "Voss Veksel- og Landmandsbank"),
        ("WAWI",   "Wallenius Wilhelmsen"),
        ("WSTEP",  "Webstep"),
        ("WWI",    "Wilh. Wilhelmsen Holding Ser. A"),
        ("WWIB",   "Wilh. Wilhelmsen Holding Ser. B"),
        ("YAR",    "Yara International"),
        ("ZAL",    "Zalaris"),
        ("ZAP",    "Zaptec ASA"),
        ("ZLNA",   "Zelluna"),
    ];

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
        var existingSymbols = await db.Assets
            .Where(a => a.Market == "OSE")
            .Select(a => a.Symbol)
            .ToHashSetAsync();

        var toInsert = Tickers
            .Where(t => !existingSymbols.Contains(t.Symbol))
            .Select(t => new Asset
            {
                Id           = AppDbContext.AssetGuid(t.Symbol),
                Name         = t.Name,
                Type         = AssetType.Stock,
                Symbol       = t.Symbol,
                Market       = "OSE",
                Broker       = "NordNet",
                BrokerSymbol = NordnetSlug(t.Name, t.Symbol),
            })
            .ToList();

        if (toInsert.Count == 0) return;

        db.Assets.AddRange(toInsert);
        await db.SaveChangesAsync();
    }
}
