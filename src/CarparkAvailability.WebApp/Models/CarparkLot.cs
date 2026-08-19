namespace CarparkAvailability.WebApp.Models;

public sealed class CarparkLot
{
    public string LotType { get; init; } = string.Empty;
    public int TotalLots { get; init; }
    public int LotsAvailable { get; init; }
}
