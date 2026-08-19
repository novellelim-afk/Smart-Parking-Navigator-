using System.Globalization;
using System.Text;
using CarparkAvailability.ApiApp.Models;
using Microsoft.Extensions.Options;

namespace CarparkAvailability.ApiApp.Services;

public sealed class CsvIngestionService
{
    private static readonly string[] RequiredColumns =
    [
        "car_park_no",
        "address",
        "x_coord",
        "y_coord",
        "car_park_type",
        "type_of_parking_system",
        "short_term_parking",
        "free_parking",
        "night_parking",
        "car_park_decks",
        "gantry_height",
        "car_park_basement"
    ];

    private readonly ILogger<CsvIngestionService> _logger;

    public CsvIngestionService(IHostEnvironment environment, IConfiguration configuration, ILogger<CsvIngestionService> logger)
    {
        _logger = logger;
        var csvRelativePath = configuration["StaticData:CsvPath"] ?? "Data/HDBCarparkInformation.csv";
        var csvPath = Path.Combine(environment.ContentRootPath, csvRelativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException($"Required HDB CSV file was not found at '{csvPath}'.", csvPath);
        }

        Records = LoadRecords(csvPath);
    }

    public IReadOnlyDictionary<string, HdbCarparkRecord> Records { get; }

    private IReadOnlyDictionary<string, HdbCarparkRecord> LoadRecords(string csvPath)
    {
        var lines = File.ReadAllLines(csvPath);
        if (lines.Length == 0)
        {
            return new Dictionary<string, HdbCarparkRecord>(StringComparer.OrdinalIgnoreCase);
        }

        var header = ParseCsvLine(lines[0]);
        ValidateHeader(header);
        var indexByName = header
            .Select((name, index) => new { Name = name.Trim(), Index = index })
            .ToDictionary(item => item.Name, item => item.Index, StringComparer.OrdinalIgnoreCase);

        var records = new Dictionary<string, HdbCarparkRecord>(StringComparer.OrdinalIgnoreCase);

        for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var values = ParseCsvLine(line);
                if (values.Count < header.Count)
                {
                    _logger.LogWarning("Skipping CSV row {LineNumber}: expected {Expected} columns but found {Actual}.", lineIndex + 1, header.Count, values.Count);
                    continue;
                }

                var carparkNo = GetValue(values, indexByName, "car_park_no").Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(carparkNo))
                {
                    continue;
                }

                if (!TryParseDouble(GetValue(values, indexByName, "x_coord"), out var xCoord)
                    || !TryParseDouble(GetValue(values, indexByName, "y_coord"), out var yCoord))
                {
                    _logger.LogWarning("Skipping CSV row {LineNumber}: invalid SVY21 coordinates for car park {CarparkNo}.", lineIndex + 1, carparkNo);
                    continue;
                }

                var (latitude, longitude) = Svy21Converter.Convert(xCoord, yCoord);
                if (latitude is < 1.0 or > 1.6 || longitude is < 103.0 or > 104.5)
                {
                    _logger.LogWarning("Skipping CSV row {LineNumber}: converted coordinates out of Singapore bounds for car park {CarparkNo}.", lineIndex + 1, carparkNo);
                    continue;
                }

                var record = new HdbCarparkRecord
                {
                    CarparkNo = carparkNo,
                    Address = GetValue(values, indexByName, "address").Trim(),
                    XCoord = xCoord,
                    YCoord = yCoord,
                    CarparkType = GetValue(values, indexByName, "car_park_type").Trim(),
                    TypeOfParkingSystem = GetValue(values, indexByName, "type_of_parking_system").Trim(),
                    ShortTermParking = GetValue(values, indexByName, "short_term_parking").Trim(),
                    FreeParking = GetValue(values, indexByName, "free_parking").Trim(),
                    NightParking = GetValue(values, indexByName, "night_parking").Trim(),
                    CarparkDecks = ParseInt(GetValue(values, indexByName, "car_park_decks")),
                    GantryHeight = ParseDouble(GetValue(values, indexByName, "gantry_height")),
                    CarparkBasement = string.Equals(GetValue(values, indexByName, "car_park_basement").Trim(), "Y", StringComparison.OrdinalIgnoreCase),
                    Latitude = latitude,
                    Longitude = longitude
                };

                records[carparkNo] = record;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Skipping CSV row {LineNumber} due to a parse error.", lineIndex + 1);
            }
        }

        return records;
    }

    private static string GetValue(IReadOnlyList<string> values, IReadOnlyDictionary<string, int> indexByName, string columnName)
        => values[indexByName[columnName]];

    private static void ValidateHeader(IReadOnlyCollection<string> header)
    {
        foreach (var requiredColumn in RequiredColumns)
        {
            if (!header.Contains(requiredColumn, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"CSV header is missing required column '{requiredColumn}'.");
            }
        }
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];

            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    builder.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (character == ',' && !inQuotes)
            {
                values.Add(builder.ToString());
                builder.Clear();
                continue;
            }

            builder.Append(character);
        }

        values.Add(builder.ToString());
        return values;
    }

    private static bool TryParseDouble(string input, out double value)
        => double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static int ParseInt(string input)
        => int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static double ParseDouble(string input)
        => double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
