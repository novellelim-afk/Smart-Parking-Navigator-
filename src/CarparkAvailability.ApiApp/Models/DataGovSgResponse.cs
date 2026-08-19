using System.Text.Json.Serialization;

namespace CarparkAvailability.ApiApp.Models;

public sealed class DataGovSgResponse
{
    [JsonPropertyName("items")]
    public List<DataGovSgItem> Items { get; init; } = [];
}

public sealed class DataGovSgItem
{
    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; init; }

    [JsonPropertyName("carpark_data")]
    public List<DataGovSgCarparkData> CarparkData { get; init; } = [];
}

public sealed class DataGovSgCarparkData
{
    [JsonPropertyName("carpark_number")]
    public string? CarparkNumber { get; init; }

    [JsonPropertyName("update_datetime")]
    public string? UpdateDatetime { get; init; }

    [JsonPropertyName("carpark_info")]
    public List<DataGovSgCarparkInfo> CarparkInfo { get; init; } = [];
}

public sealed class DataGovSgCarparkInfo
{
    [JsonPropertyName("lot_type")]
    public string? LotType { get; init; }

    [JsonPropertyName("total_lots")]
    public string? TotalLots { get; init; }

    [JsonPropertyName("lots_available")]
    public string? LotsAvailable { get; init; }
}
