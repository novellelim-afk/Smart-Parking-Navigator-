using CarparkAvailability.ApiApp.Models;

namespace CarparkAvailability.ApiApp.Services;

public sealed class CarparkService
{
    private readonly CsvIngestionService _csvIngestionService;
    private readonly AvailabilityStore _availabilityStore;

    public CarparkService(CsvIngestionService csvIngestionService, AvailabilityStore availabilityStore)
    {
        _csvIngestionService = csvIngestionService;
        _availabilityStore = availabilityStore;
    }

    public CarparkAvailabilityResponse GetNearby(double lat, double lng, int radiusMetres)
    {
        var snapshot = _availabilityStore.GetSnapshot();
        var results = _csvIngestionService.Records.Values
            .Select(record => CreateResult(record, snapshot, HaversineCalculator.DistanceMetres(lat, lng, record.Latitude, record.Longitude)))
            .Where(result => result.DistanceMetres <= radiusMetres)
            .OrderBy(result => result.DistanceMetres)
            .Take(20)
            .ToList();

        return new CarparkAvailabilityResponse
        {
            Timestamp = snapshot.LastPollTimestamp,
            Freshness = snapshot.Freshness,
            Carparks = results
        };
    }

    public CarparkResult? GetByNo(string carparkNo)
    {
        if (!_csvIngestionService.Records.TryGetValue(carparkNo.Trim().ToUpperInvariant(), out var record))
        {
            return null;
        }

        return CreateResult(record, _availabilityStore.GetSnapshot(), 0);
    }

    private static CarparkResult CreateResult(HdbCarparkRecord record, AvailabilitySnapshot snapshot, double distanceMetres)
    {
        var hasAvailability = snapshot.Availability.TryGetValue(record.CarparkNo, out var lots);
        snapshot.UpdateTimestamps.TryGetValue(record.CarparkNo, out var updateDatetime);
        var freshness = GetCarparkFreshness(updateDatetime, hasAvailability && (lots?.Count ?? 0) > 0);

        return new CarparkResult
        {
            CarparkNo = record.CarparkNo,
            Address = record.Address,
            Latitude = record.Latitude,
            Longitude = record.Longitude,
            DistanceMetres = Math.Round(distanceMetres, MidpointRounding.AwayFromZero),
            CarparkType = record.CarparkType,
            TypeOfParkingSystem = record.TypeOfParkingSystem,
            ShortTermParking = record.ShortTermParking,
            FreeParking = record.FreeParking,
            NightParking = record.NightParking,
            CarparkDecks = record.CarparkDecks,
            GantryHeight = record.GantryHeight,
            CarparkBasement = record.CarparkBasement,
            Lots = lots ?? [],
            UpdateDatetime = updateDatetime,
            Freshness = freshness
        };
    }

    private static string GetCarparkFreshness(DateTimeOffset? updateDatetime, bool hasAvailability)
    {
        if (!hasAvailability || !updateDatetime.HasValue)
        {
            return "unavailable";
        }

        return AvailabilityStore.SingaporeNow() - updateDatetime.Value <= TimeSpan.FromMinutes(5)
            ? "live"
            : "stale";
    }
}
