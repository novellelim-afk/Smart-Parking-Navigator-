using CarparkAvailability.ApiApp;
using CarparkAvailability.ApiApp.Models;
using CarparkAvailability.ApiApp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CarparkAvailability.Tests;

public class Svy21ConverterTests
{
    [Fact]
    public void Convert_KnownAcbCarpark_ReturnsApproximateWgs84()
    {
        var (lat, lng) = Svy21Converter.Convert(30314.7936, 31490.4942);

        Assert.InRange(lat, 1.29, 1.31);
        Assert.InRange(lng, 103.85, 103.87);
    }

    [Fact]
    public void Convert_SlaOrigin_MapsToCorrectCoordinates()
    {
        var (lat, lng) = Svy21Converter.Convert(28001.642, 38744.572);

        Assert.InRange(lat, 1.366, 1.368);
        Assert.InRange(lng, 103.832, 103.834);
    }

    [Fact]
    public void Convert_ThrowsForNaN()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Svy21Converter.Convert(double.NaN, 38000));
    }
}

public class HaversineCalculatorTests
{
    [Fact]
    public void DistanceMetres_SamePoint_ReturnsZero()
    {
        var distance = HaversineCalculator.DistanceMetres(1.3521, 103.8198, 1.3521, 103.8198);
        Assert.Equal(0.0, distance, 3);
    }

    [Fact]
    public void DistanceMetres_KnownDistance_IsAccurate()
    {
        var distance = HaversineCalculator.DistanceMetres(1.2948, 103.8534, 1.2868, 103.8545);
        Assert.InRange(distance, 800, 950);
    }

    [Fact]
    public void DistanceMetres_FiveHundredMeter_BoundaryCheck()
    {
        var distance = HaversineCalculator.DistanceMetres(1.3521, 103.8198, 1.3566, 103.8198);
        Assert.InRange(distance, 490, 510);
    }
}

public class AvailabilityStoreTests
{
    [Fact]
    public void GetSnapshot_InitialState_ReturnsUnavailable()
    {
        var store = new AvailabilityStore();
        var snapshot = store.GetSnapshot();
        Assert.Equal("unavailable", snapshot.Freshness);
        Assert.Null(snapshot.LastPollTimestamp);
    }

    [Fact]
    public void GetSnapshot_AfterRecentUpdate_ReturnsLive()
    {
        var store = new AvailabilityStore();
        var now = DateTimeOffset.UtcNow;
        store.Update(
            new Dictionary<string, List<CarparkLot>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, DateTimeOffset?>(StringComparer.OrdinalIgnoreCase),
            now,
            "live");

        var snapshot = store.GetSnapshot();
        Assert.Equal("live", snapshot.Freshness);
        Assert.Equal(now, snapshot.LastPollTimestamp);
    }

    [Fact]
    public void MarkFailure_WithPreviousTimestamp_ReturnsStale()
    {
        var store = new AvailabilityStore();
        store.Update(
            new Dictionary<string, List<CarparkLot>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, DateTimeOffset?>(StringComparer.OrdinalIgnoreCase),
            DateTimeOffset.UtcNow,
            "live");

        store.MarkFailure();

        var snapshot = store.GetSnapshot();
        Assert.Equal("stale", snapshot.Freshness);
    }

    [Fact]
    public void MarkFailure_WithoutPreviousTimestamp_ReturnsUnavailable()
    {
        var store = new AvailabilityStore();
        store.MarkFailure();

        var snapshot = store.GetSnapshot();
        Assert.Equal("unavailable", snapshot.Freshness);
    }
}

public class CsvIngestionServiceTests
{
    private static IConfiguration BuildConfig(string csvFileName)
    {
        var env = new Dictionary<string, string?> { ["StaticData__CsvPath"] = csvFileName };
        return new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
    }

    private static CsvIngestionService CreateFromCsvContent(string csvContent, string? csvFileName = null)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var filename = csvFileName ?? "test.csv";
        var csvPath = Path.Combine(tempDir, filename);
        File.WriteAllText(csvPath, csvContent);

        var env = new FakeHostEnvironment(tempDir);
        // Write a minimal appsettings-like JSON to configure csv path
        var configJson = $"{{\"StaticData\":{{\"CsvPath\":\"{filename}\"}}}}";
        var configFile = Path.Combine(tempDir, "appsettings.test.json");
        File.WriteAllText(configFile, configJson);

        var config = new ConfigurationBuilder()
            .AddJsonFile(configFile, optional: false)
            .Build();

        var logger = NullLogger<CsvIngestionService>.Instance;
        return new CsvIngestionService(env, config, logger);
    }

    [Fact]
    public void ParseCsvLine_QuotedFieldWithComma_IsHandledCorrectly()
    {
        var csvContent = "car_park_no,address,x_coord,y_coord,car_park_type,type_of_parking_system,short_term_parking,free_parking,night_parking,car_park_decks,gantry_height,car_park_basement\r\n" +
                         "ACB,\"SOME, ADDRESS\",30314.7936,31490.4942,BASEMENT CAR PARK,ELECTRONIC PARKING,WHOLE DAY,NO,YES,1,1.8,Y\r\n";

        var service = CreateFromCsvContent(csvContent);

        Assert.True(service.Records.ContainsKey("ACB"));
        Assert.Equal("SOME, ADDRESS", service.Records["ACB"].Address);
    }

    [Fact]
    public void CsvIngestion_ValidRow_PopulatesAllFields()
    {
        var csvContent = "car_park_no,address,x_coord,y_coord,car_park_type,type_of_parking_system,short_term_parking,free_parking,night_parking,car_park_decks,gantry_height,car_park_basement\r\n" +
                         "ACB,BLK 270/271 ALBERT CENTRE,30314.7936,31490.4942,BASEMENT CAR PARK,ELECTRONIC PARKING,WHOLE DAY,NO,YES,1,1.8,Y\r\n";

        var service = CreateFromCsvContent(csvContent);

        var record = service.Records["ACB"];
        Assert.Equal("ACB", record.CarparkNo);
        Assert.Equal("BLK 270/271 ALBERT CENTRE", record.Address);
        Assert.Equal("BASEMENT CAR PARK", record.CarparkType);
        Assert.Equal("YES", record.NightParking);
        Assert.Equal(1, record.CarparkDecks);
        Assert.Equal(1.8, record.GantryHeight);
        Assert.True(record.CarparkBasement);
        Assert.InRange(record.Latitude, 1.15, 1.48);
        Assert.InRange(record.Longitude, 103.58, 104.09);
    }

    [Fact]
    public void CsvIngestion_InvalidCoordinates_RowSkipped()
    {
        var csvContent = "car_park_no,address,x_coord,y_coord,car_park_type,type_of_parking_system,short_term_parking,free_parking,night_parking,car_park_decks,gantry_height,car_park_basement\r\n" +
                         "BAD,BAD ADDR,notanumber,notanumber,SURFACE CAR PARK,ELECTRONIC PARKING,WHOLE DAY,NO,NO,0,0,N\r\n";

        var service = CreateFromCsvContent(csvContent);

        Assert.False(service.Records.ContainsKey("BAD"));
    }

    [Fact]
    public void CsvIngestion_MissingFile_Throws()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var env = new FakeHostEnvironment(tempDir);

        var configJson = "{\"StaticData\":{\"CsvPath\":\"nonexistent.csv\"}}";
        var configFile = Path.Combine(tempDir, "appsettings.test.json");
        File.WriteAllText(configFile, configJson);

        var config = new ConfigurationBuilder().AddJsonFile(configFile).Build();
        var logger = NullLogger<CsvIngestionService>.Instance;

        Assert.Throws<FileNotFoundException>(() =>
        {
            _ = new CsvIngestionService(env, config, logger);
        });
    }

    [Fact]
    public void CsvIngestion_MissingRequiredColumn_Throws()
    {
        var csvContent = "car_park_no,address,x_coord\r\nACB,TEST,30314.79\r\n";
        Assert.Throws<InvalidOperationException>(() => CreateFromCsvContent(csvContent));
    }
}

public class FilterTests
{
    private static CarparkResult MakeCarpark(string no, int lotsAvailable, string nightParking, string carparkType, string lotType = "C") =>
        new()
        {
            CarparkNo = no,
            Address = no,
            Latitude = 1.35,
            Longitude = 103.82,
            DistanceMetres = 100,
            CarparkType = carparkType,
            TypeOfParkingSystem = "ELECTRONIC PARKING",
            ShortTermParking = "WHOLE DAY",
            FreeParking = "NO",
            NightParking = nightParking,
            CarparkDecks = 1,
            GantryHeight = 2.0,
            CarparkBasement = false,
            Lots = [new CarparkLot { LotType = lotType, TotalLots = 100, LotsAvailable = lotsAvailable }],
            UpdateDatetime = DateTimeOffset.UtcNow,
            Freshness = "live"
        };

    [Fact]
    public void AvailableOnlyFilter_RemovesCarparkWithZeroLots()
    {
        var carparks = new List<CarparkResult>
        {
            MakeCarpark("A1", 5, "YES", "SURFACE CAR PARK"),
            MakeCarpark("A2", 0, "NO", "MULTI-STOREY CAR PARK"),
        };

        var filtered = carparks.Where(c => c.Lots.Any(l => l.LotType == "C" && l.LotsAvailable > 0)).ToList();

        Assert.Single(filtered);
        Assert.Equal("A1", filtered[0].CarparkNo);
    }

    [Fact]
    public void NightParkingFilter_OnlyReturnsNightParking()
    {
        var carparks = new List<CarparkResult>
        {
            MakeCarpark("A1", 5, "YES", "SURFACE CAR PARK"),
            MakeCarpark("A2", 5, "NO", "MULTI-STOREY CAR PARK"),
        };

        var filtered = carparks.Where(c => string.Equals(c.NightParking, "YES", StringComparison.OrdinalIgnoreCase)).ToList();

        Assert.Single(filtered);
        Assert.Equal("A1", filtered[0].CarparkNo);
    }

    [Fact]
    public void CarparkTypeFilter_OnlyMatchingType()
    {
        var carparks = new List<CarparkResult>
        {
            MakeCarpark("A1", 5, "YES", "SURFACE CAR PARK"),
            MakeCarpark("A2", 5, "NO", "MULTI-STOREY CAR PARK"),
            MakeCarpark("A3", 5, "NO", "BASEMENT CAR PARK"),
        };

        var filtered = carparks.Where(c => c.CarparkType == "MULTI-STOREY CAR PARK").ToList();

        Assert.Single(filtered);
        Assert.Equal("A2", filtered[0].CarparkNo);
    }

    [Fact]
    public void MultipleFilters_CombinedCorrectly()
    {
        var carparks = new List<CarparkResult>
        {
            MakeCarpark("A1", 5, "YES", "SURFACE CAR PARK"),
            MakeCarpark("A2", 0, "YES", "SURFACE CAR PARK"),
            MakeCarpark("A3", 5, "NO", "SURFACE CAR PARK"),
        };

        var filtered = carparks
            .Where(c => string.Equals(c.NightParking, "YES", StringComparison.OrdinalIgnoreCase))
            .Where(c => c.Lots.Any(l => l.LotType == "C" && l.LotsAvailable > 0))
            .ToList();

        Assert.Single(filtered);
        Assert.Equal("A1", filtered[0].CarparkNo);
    }
}

internal sealed class FakeHostEnvironment(string contentRootPath) : Microsoft.Extensions.Hosting.IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "Tests";
    public string ContentRootPath { get; set; } = contentRootPath;
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
        new Microsoft.Extensions.FileProviders.PhysicalFileProvider(contentRootPath);
}
