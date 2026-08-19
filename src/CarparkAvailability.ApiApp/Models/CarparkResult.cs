namespace CarparkAvailability.ApiApp.Models;

public sealed class CarparkResult
{
    public required string CarparkNo { get; init; }
    public required string Address { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required double DistanceMetres { get; init; }
    public required string CarparkType { get; init; }
    public required string TypeOfParkingSystem { get; init; }
    public required string ShortTermParking { get; init; }
    public required string FreeParking { get; init; }
    public required string NightParking { get; init; }
    public required int CarparkDecks { get; init; }
    public required double GantryHeight { get; init; }
    public required bool CarparkBasement { get; init; }
    public required List<CarparkLot> Lots { get; init; }
    public required DateTimeOffset? UpdateDatetime { get; init; }
    public required string Freshness { get; init; }
}
