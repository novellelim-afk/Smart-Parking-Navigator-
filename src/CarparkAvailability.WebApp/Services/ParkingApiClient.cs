using System.Text.Json;
using CarparkAvailability.WebApp.Models;
using System.Globalization;

namespace CarparkAvailability.WebApp.Services;

public sealed class ParkingApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public ParkingApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CarparkAvailabilityResponse?> GetNearbyAsync(double lat, double lng)
    {
        var latitude = lat.ToString("F6", CultureInfo.InvariantCulture);
        var longitude = lng.ToString("F6", CultureInfo.InvariantCulture);
        using var response = await _httpClient.GetAsync($"/api/carparks?lat={latitude}&lng={longitude}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<CarparkAvailabilityResponse>(stream, JsonOptions);
    }

    public async Task<CarparkResult?> GetDetailAsync(string carparkNo)
    {
        using var response = await _httpClient.GetAsync($"/api/carparks/{Uri.EscapeDataString(carparkNo)}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<CarparkResult>(stream, JsonOptions);
    }
}
