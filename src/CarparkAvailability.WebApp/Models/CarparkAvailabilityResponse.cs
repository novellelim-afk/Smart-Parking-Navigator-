namespace CarparkAvailability.WebApp.Models;

public sealed class CarparkAvailabilityResponse
{
    public DateTimeOffset? Timestamp { get; init; }
    public string Freshness { get; init; } = "unavailable";
    public List<CarparkResult> Carparks { get; init; } = [];
}
