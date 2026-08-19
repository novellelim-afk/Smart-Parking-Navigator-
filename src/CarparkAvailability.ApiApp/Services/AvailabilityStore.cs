using CarparkAvailability.ApiApp.Models;

namespace CarparkAvailability.ApiApp.Services;

public sealed class AvailabilityStore
{
    private readonly Lock _syncLock = new();
    private Dictionary<string, List<CarparkLot>> _availability = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, DateTimeOffset?> _updateTimestamps = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset? _lastPollTimestamp;
    private string _freshness = "unavailable";

    public void Update(Dictionary<string, List<CarparkLot>> availability, Dictionary<string, DateTimeOffset?> updateTimestamps, DateTimeOffset pollTimestamp, string freshness)
    {
        lock (_syncLock)
        {
            _availability = CloneAvailability(availability);
            _updateTimestamps = new Dictionary<string, DateTimeOffset?>(updateTimestamps, StringComparer.OrdinalIgnoreCase);
            _lastPollTimestamp = pollTimestamp;
            _freshness = freshness;
        }
    }

    public void MarkFailure()
    {
        lock (_syncLock)
        {
            _freshness = _lastPollTimestamp.HasValue ? "stale" : "unavailable";
        }
    }

    public AvailabilitySnapshot GetSnapshot()
    {
        lock (_syncLock)
        {
            var freshness = CalculateFreshness(_lastPollTimestamp, _freshness);
            return new AvailabilitySnapshot(
                CloneAvailability(_availability),
                new Dictionary<string, DateTimeOffset?>(_updateTimestamps, StringComparer.OrdinalIgnoreCase),
                _lastPollTimestamp,
                freshness);
        }
    }

    private static Dictionary<string, List<CarparkLot>> CloneAvailability(IReadOnlyDictionary<string, List<CarparkLot>> availability)
        => availability.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.Select(lot => new CarparkLot
            {
                LotType = lot.LotType,
                TotalLots = lot.TotalLots,
                LotsAvailable = lot.LotsAvailable
            }).ToList(),
            StringComparer.OrdinalIgnoreCase);

    private static string CalculateFreshness(DateTimeOffset? lastPollTimestamp, string currentFreshness)
    {
        if (!lastPollTimestamp.HasValue)
        {
            return "unavailable";
        }

        if (currentFreshness == "stale")
        {
            return "stale";
        }

        return SingaporeNow() - lastPollTimestamp.Value <= TimeSpan.FromMinutes(5)
            ? "live"
            : "stale";
    }

    public static DateTimeOffset SingaporeNow()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Singapore");
        return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
    }
}

public sealed record AvailabilitySnapshot(
    Dictionary<string, List<CarparkLot>> Availability,
    Dictionary<string, DateTimeOffset?> UpdateTimestamps,
    DateTimeOffset? LastPollTimestamp,
    string Freshness);
