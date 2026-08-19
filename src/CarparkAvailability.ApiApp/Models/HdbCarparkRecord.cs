namespace CarparkAvailability.ApiApp.Models;

public sealed class HdbCarparkRecord
{
    public required string CarparkNo { get; init; }
    public required string Address { get; init; }
    public required double XCoord { get; init; }
    public required double YCoord { get; init; }
    public required string CarparkType { get; init; }
    public required string TypeOfParkingSystem { get; init; }
    public required string ShortTermParking { get; init; }
    public required string FreeParking { get; init; }
    public required string NightParking { get; init; }
    public required int CarparkDecks { get; init; }
    public required double GantryHeight { get; init; }
    public required bool CarparkBasement { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
}
