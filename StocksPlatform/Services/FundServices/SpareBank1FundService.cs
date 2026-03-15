using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StocksPlatform.Services.FundServices;

/// <summary>
/// Fetches fund holdings from the SpareBank 1 open API.
///   https://www.sparebank1.no/openapi/personal/banking/fund/products/details/{isin}
/// </summary>
public class SpareBank1FundService(HttpClient http) : IFundHoldingsProvider
{
    private const string BaseUrl = "https://www.sparebank1.no/openapi/personal/banking/fund/products/details";

    private static readonly IReadOnlyList<string> _fundIds =
    [
        "SE0024367015",
        "NO0010921349",
        "NO0010921356",
        "NO0010921323",
        "NO0010921331",
        "NO0010921307",
        "NO0010921315",
        "NO0010924913",
        "NO0011151805",
        "NO0010763915",
        "NO0010732852",
        "NO0010763881",
        "NO0010748213",
        "SE0013693280",
        "NO0010748304",
        "NO0010775729",
        "NO0010904857",
        "NO0010752835",
        "NO0010856370",
        "LU0248183658",
        // "NO0010817505",
        // "NO0010817703",
        // "NO0010817828",
        // "NO0010907363",
        // "NO0010907355",
        // "NO0010904865",
        // "SE0015345871",
        // "LU1165135879",
        // "LU0406803147",
        // "LU0347712274",
        // "SE0018013633",
        // "NO0010817372",
        // "NO0010817448",
        // "NO0010817760",
        // "NO0010337421",
        // "NO0010877707",
        // "NO0013528299",
        // "NO0008000593",
        // "LU2075955943",
        // "LU2090050936",
        // "LU2090050696",
        // "NO0010820046",
        // "NO0010849524",
        // "NO0010337512",
        // "NO0010801418",
        // "NO0010102866",
        // "NO0010337579",
        // "NO0010102890",
        // "NO0010820012",
        // "NO0008000601",
        // "NO0010819915",
        // "NO0010849607",
        // "NO0010819972",
        // "NO0013413831",
        // "NO0010337819",
        // "NO0010337678",
        // "NO0010337942",
        // "NO0010856354",
        // "NO0010774409",
        // "NO0010752793",
        // "NO0012444514",
        // "NO0012445289",
        // "NO0012445263",
        // "NO0012444597",
        // "NO0012447137",
        // "NO0013070326",
        // "NO0013071605",
        // "NO0013071738",
        // "NO0013071878",
        // "NO0013072124",
        // "NO0013072173",
        // "NO0013072363",
        // "LU0106820292",
        // "LU0968301142",
        // "LU1799645038",
        // "LU0323592138",
        // "LU0248185604",
        // "NO0010735137",
        // "NO0010679012",
        // "NO0010679038",
        // "NO0010708712",
        // "NO0010678998",
        // "SE0024367023",
        // "NO0013072439",
        // "NO0013072447",
        // "NO0013072454",
        // "NO0010817661",
        // "NO0010817562",
        // "NO0010817786",
        // "NO0010849169",
        // "NO0011110256",
        // "NO0010817836",
        // "NO0010089444",
        // "NO0010105489",
        // "NO0010089501",
        // "NO0010032055",
        // "NO0010833395",
        // "NO0010089402",
        // "IE00BYQ7ZL84",
        // "LU2572687130",
        // "LU2572685357",
        // "NO0010317282",
        // "NO0010039670",
        // "NO0010039688",
        // "LU0302237721",
        // "LU0067059799",
        // "LU1941809938",
        // "LU2437453066",
        // "LU2437452928",
        // "LU2437452845",
        // "NO0010212350",
        // "NO0010126030",
        // "NO0010075476",
        // "NO0008001880",
        // "NO0010199086",
        // "NO0010003999",
        // "NO0010047194",
        // "NO0010165764",
        // "LU0231203729",
        // "NO0010073232",
        // "NO0010072945",
        // "NO0010073224",
        // "NO0010730179",
        // "NO0010073216",
        // "NO0010693864",
        // "NO0010821614",
        // "NO0010861933",
        // "NO0010272388",
        // "NO0010662836",
        // "LU2794645759",
        // "LU0705259843",
        // "FI4000566146",
        // "FI4000349428",
        // "FI0008813290",
        // "FI0008813316",
        // "LU0348926360",
        // "FI4000565973",
        // "FI4000020748",
        // "FI4000565841",
        // "FI0008813258",
        // "NO0010922859",
        // "NO0010732860",
        // "NO0010732878",
        // "NO0010705908",
        // "NO0011151706",
        // "NO0011151730",
        // "NO0010763899",
        // "NO0010763907",
        // "NO0010028962",
        // "NO0010732837",
        // "NO0010732845",
        // "NO0010028988",
        // "NO0010763865",
        // "NO0010763873",
        // "NO0008000155",
        // "NO0010748197",
        // "NO0010748205",
        // "NO0008000379",
        // "SE0013668142",
        // "SE0013668175",
        // "SE0013693264",
        // "NO0010748288",
        // "NO0010748296",
        // "NO0008000023",
        // "NO0010775695",
        // "NO0010775703",
        // "NO0010775711",
        // "NO0010297898",
        // "NO0010660434",
        // "LU0994294022",
        // "LU0994294378",
        // "LU0994294535",
        // "LU0994294709",
        // "LU0994294964",
        // "LU0994295185",
        // "IE00BD4TR802",
        // "NO0010735129",
        // "NO0008004009",
        // "NO0010140502",
        // "NO0010657356",
        // "NO0008000445",
        // "NO0010814486",
        // "NO0010814437",
        // "NO0010814411",
        // "NO0010814429",
        // "NO0010818560",
        // "NO0010818552",
        // "NO0010675267",
        // "NO0010675275",
        // "NO0010814478",
        // "NO0010814460",
        // "NO0010814445",
        // "NO0010814452",
        // "NO0010346422",
        // "NO0010788292",
        // "NO0010657273",
        // "NO0008000973",
        // "NO0008000783",
        // "NO0010849151",
        // "NO0010788284",
        // "NO0011110249",
        // "NO0010883465",
        // "NO0008000999",
        // "NO0008000841",
        // "LU0128522157",
        // "LU0052750758",
        // "LU3011353193",
        // "LU3011353789",
        // "LU3011350769",
        // "LU3011352468",
        // "LU3011352625",
        // "DK0060607810",
        // "NO0011029365",
        // "NO0011031429",
        // "NO0011031486",
        // "NO0011031510",
        // "NO0011031536",
        // "SE0025158967",
    ];

    public IReadOnlyList<string> FundIds => _fundIds;

    public async Task<IReadOnlyList<FundHoldingResult>> GetAllHoldingsAsync()
    {
        var results = new List<FundHoldingResult>(_fundIds.Count);

        foreach (var isin in _fundIds)
        {
            Sb1FundDetails? details;
            try
            {
                details = await http.GetFromJsonAsync<Sb1FundDetails>(
                    $"{BaseUrl}/{Uri.EscapeDataString(isin)}");
            }
            catch { continue; }
            if (details is null) continue;

            var holdings = details.Holdings
                .Where(h => !string.IsNullOrEmpty(h.Isin))
                .Select(h => new FundHoldingEntry(h.Isin!, h.Percent))
                .ToList();

            results.Add(new FundHoldingResult(isin, details.LastUpdated?.Portfolio, holdings));
        }

        return results;
    }

    // SpareBank 1-specific response DTOs — private to this service.
    private sealed class Sb1FundDetails
    {
        [JsonPropertyName("holdings")]
        public List<Sb1Holding> Holdings { get; init; } = [];

        [JsonPropertyName("lastUpdated")]
        public Sb1LastUpdated? LastUpdated { get; init; }
    }

    private sealed class Sb1Holding
    {
        [JsonPropertyName("percent")]
        public double Percent { get; init; }

        [JsonPropertyName("isin")]
        public string? Isin { get; init; }
    }

    private sealed class Sb1LastUpdated
    {
        [JsonPropertyName("portfolio")]
        public string? Portfolio { get; init; }
    }
}
