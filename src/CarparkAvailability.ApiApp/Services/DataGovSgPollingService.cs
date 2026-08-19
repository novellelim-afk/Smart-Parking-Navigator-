using System.Globalization;
using System.Text.Json;
using CarparkAvailability.ApiApp.Models;

namespace CarparkAvailability.ApiApp.Services;

public sealed class DataGovSgPollingService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly AvailabilityStore _availabilityStore;
    private readonly ILogger<DataGovSgPollingService> _logger;
    private readonly string _sampleFilePath;
    private readonly string? _apiKey;
    private bool _usingMockData;

    public DataGovSgPollingService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IHostEnvironment environment,
        AvailabilityStore availabilityStore,
        ILogger<DataGovSgPollingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _environment = environment;
        _availabilityStore = availabilityStore;
        _logger = logger;
        _apiKey = configuration["DataGovSg__ApiKey"] ?? configuration["DataGovSg:ApiKey"];
        _sampleFilePath = Path.Combine(environment.ContentRootPath, "Data", "carpark-availability-sample.json");
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!IsRealApiKeyConfigured())
        {
            _usingMockData = true;
            _logger.LogWarning("DataGovSg:ApiKey not configured; using mock availability data from sample file.");
        }

        await PollAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PollAsync(stoppingToken);
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = _usingMockData
                ? await LoadMockResponseAsync(cancellationToken)
                : await LoadLiveResponseAsync(cancellationToken);

            if (response is null)
            {
                _availabilityStore.MarkFailure();
                return;
            }

            var item = response.Items.FirstOrDefault();
            var availability = new Dictionary<string, List<CarparkLot>>(StringComparer.OrdinalIgnoreCase);
            var updateTimestamps = new Dictionary<string, DateTimeOffset?>(StringComparer.OrdinalIgnoreCase);

            foreach (var carparkData in item?.CarparkData ?? [])
            {
                var carparkNo = carparkData.CarparkNumber?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(carparkNo))
                {
                    continue;
                }

                availability[carparkNo] = carparkData.CarparkInfo.Select(info => new CarparkLot
                {
                    LotType = (info.LotType ?? string.Empty).Trim().ToUpperInvariant(),
                    TotalLots = ParseLots(info.TotalLots, carparkNo, nameof(info.TotalLots)),
                    LotsAvailable = ParseLots(info.LotsAvailable, carparkNo, nameof(info.LotsAvailable))
                }).ToList();
                updateTimestamps[carparkNo] = ParseTimestamp(carparkData.UpdateDatetime);
            }

            _availabilityStore.Update(availability, updateTimestamps, AvailabilityStore.SingaporeNow(), "live");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to refresh car park availability data.");
            _availabilityStore.MarkFailure();
        }
    }

    private async Task<DataGovSgResponse?> LoadLiveResponseAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("datagov");
        using var request = new HttpRequestMessage(HttpMethod.Get, "transport/carpark-availability");
        request.Headers.TryAddWithoutValidation("x-api-key", _apiKey);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("data.gov.sg availability request failed with status code {StatusCode}.", (int)response.StatusCode);
            return null;
        }

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<DataGovSgResponse>(contentStream, JsonOptions, cancellationToken);
    }

    private async Task<DataGovSgResponse?> LoadMockResponseAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_sampleFilePath))
        {
            throw new FileNotFoundException($"Mock availability sample file was not found at '{_sampleFilePath}'.", _sampleFilePath);
        }

        await using var stream = File.OpenRead(_sampleFilePath);
        return await JsonSerializer.DeserializeAsync<DataGovSgResponse>(stream, JsonOptions, cancellationToken);
    }

    private bool IsRealApiKeyConfigured()
        => !string.IsNullOrWhiteSpace(_apiKey) && !_apiKey.Contains("{{", StringComparison.Ordinal);

    private int ParseLots(string? value, string carparkNo, string fieldName)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        _logger.LogWarning("Could not parse {FieldName} for car park {CarparkNo}; defaulting to 0.", fieldName, carparkNo);
        return 0;
    }

    private static DateTimeOffset? ParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var withOffset))
        {
            if (value.Contains("+", StringComparison.Ordinal) || value.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
            {
                return withOffset;
            }
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var localDateTime))
        {
            return new DateTimeOffset(localDateTime, TimeSpan.FromHours(8));
        }

        return null;
    }
}
