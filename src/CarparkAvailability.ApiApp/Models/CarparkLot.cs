namespace CarparkAvailability.ApiApp.Models;

public sealed class CarparkLot
{
    public required string LotType { get; init; }
    public required int TotalLots { get; init; }
    public required int LotsAvailable { get; init; }
}
