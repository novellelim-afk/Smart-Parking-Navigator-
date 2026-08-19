namespace CarparkAvailability.WebApp.Models;

public sealed class CarparkResult
{
    public string CarparkNo { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double DistanceMetres { get; init; }
    public string CarparkType { get; init; } = string.Empty;
    public string TypeOfParkingSystem { get; init; } = string.Empty;
    public string ShortTermParking { get; init; } = string.Empty;
    public string FreeParking { get; init; } = string.Empty;
    public string NightParking { get; init; } = string.Empty;
    public int CarparkDecks { get; init; }
    public double GantryHeight { get; init; }
    public bool CarparkBasement { get; init; }
    public List<CarparkLot> Lots { get; init; } = [];
    public DateTimeOffset? UpdateDatetime { get; init; }
    public string Freshness { get; init; } = "unavailable";
}
