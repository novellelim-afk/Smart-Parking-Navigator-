namespace CarparkAvailability.ApiApp.Models;

public sealed class CarparkAvailabilityResponse
{
    public required DateTimeOffset? Timestamp { get; init; }
    public required string Freshness { get; init; }
    public required List<CarparkResult> Carparks { get; init; }
}
