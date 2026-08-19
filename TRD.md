# Technical Requirements Document — Smart Parking Navigator

| Field        | Value                                                       |
|--------------|-------------------------------------------------------------|
| Status       | Draft                                                       |
| Version      | 1.0.0                                                       |
| Date         | 2026-08-19                                                  |
| Author       | Copilot Coding Agent                                        |
| Reviewers    | Workshop facilitators                                       |
| Issue        | [#2 Generate PRD.md and TRD.md](../../issues/2)             |
| PRD          | [PRD.md](./PRD.md) v1.0.0                                   |

---

## 1. System Overview

The application is built on the existing .NET 10 Aspire scaffold under `src/`. No new projects are to be added.

```
AppHost
 ├── ApiApp   ← calls data.gov.sg; reads HDB CSV; exposes JSON API
 └── WebApp   ← Blazor Server; calls ApiApp; renders Google Maps
ServiceDefaults (shared library)
```

AppHost orchestrates both services, injects secrets, and exposes the Aspire dashboard. ServiceDefaults registers OpenTelemetry, health checks, and service discovery for every service.

---

## 2. Component Responsibilities

### 2.1 AppHost (`CarparkAvailability.AppHost`)

- Reads `GoogleMaps:ApiKey` and `DataGovSg:ApiKey` from user secrets / environment variables via `builder.AddParameterFromConfiguration`.
- Injects `DataGovSg__ApiKey` as an environment variable into ApiApp.
- Injects `GoogleMaps__ApiKey` as an environment variable into WebApp.
- Adds a `WithReference(api)` and `WaitFor(api)` dependency from WebApp to ApiApp.
- Does **not** contain business logic.

### 2.2 ApiApp (`CarparkAvailability.ApiApp`)

Responsibilities:

| Responsibility | Detail |
|----------------|--------|
| HDB CSV ingestion | Parse `data/HDBCarparkInformation.csv` at startup; cache in memory. |
| SVY21 → WGS84 conversion | Convert `x_coord`/`y_coord` to latitude/longitude at ingestion time. |
| data.gov.sg polling | `GET /v1/transport/carpark-availability` every 60 s via background service. |
| Join | Merge live availability records with static HDB entries on `carpark_number` = `car_park_no`. |
| Distance calculation | Haversine great-circle distance from a query coordinate. |
| REST API | Expose endpoints consumed by WebApp (see section 5). |
| Last-known-good | Retain the most recent successful poll response; return it when the API is unreachable. |
| Secret handling | Read `DataGovSg__ApiKey` from environment; never log or return it. |

### 2.3 WebApp (`CarparkAvailability.WebApp`)

Responsibilities:

| Responsibility | Detail |
|----------------|--------|
| Google Maps rendering | Load the Maps JavaScript SDK using `GoogleMaps__ApiKey` from environment. |
| Destination search | Use the Places JavaScript SDK for autocomplete (client-side). |
| Blazor Server pages | Render car-park list, detail panel, filters, and freshness indicators. |
| ApiApp client | Call ApiApp endpoints via `HttpClient` with Aspire service discovery. |
| Filter state | Maintain filter state in the Blazor component; re-query or re-filter in-memory. |
| No direct external calls | WebApp must **never** call data.gov.sg. |

### 2.4 ServiceDefaults (`CarparkAvailability.ServiceDefaults`)

- Registers OpenTelemetry traces, metrics, and logs with optional OTLP export.
- Registers health-check endpoints (`/health`, `/alive`).
- Configures `HttpClient` defaults: standard resilience handler and Aspire service discovery.
- Applied to both ApiApp and WebApp via `builder.AddServiceDefaults()`.

---

## 3. Data Ingestion — HDB CSV

### 3.1 File location

`data/HDBCarparkInformation.csv` is embedded in the ApiApp project as a build-time resource or read from the filesystem path relative to the application root.

### 3.2 Schema

| CSV Column | Type | Notes |
|---|---|---|
| `car_park_no` | string | Primary key; used to join with API data. |
| `address` | string | Display label. |
| `x_coord` | decimal string | SVY21 Easting. |
| `y_coord` | decimal string | SVY21 Northing. |
| `car_park_type` | string | SURFACE / MULTI-STOREY / BASEMENT / MECHANISED |
| `type_of_parking_system` | string | ELECTRONIC / COUPON |
| `short_term_parking` | string | Displayed as-is. |
| `free_parking` | string | Displayed as-is; no time interpretation. |
| `night_parking` | string | YES / NO |
| `car_park_decks` | integer string | 0 for surface car parks. |
| `gantry_height` | decimal string | Metres; 0 when not applicable. |
| `car_park_basement` | string | Y / N |

### 3.3 Validation rules

- Header row must contain all twelve columns in any order; fail fast with a logged error if any required column is missing.
- `x_coord` and `y_coord` must be parseable as `double`; rows that fail parsing are skipped and a warning is logged.
- `car_park_decks` and `gantry_height` are parsed as numeric types; non-numeric values default to `0` with a warning.
- All other string columns are trimmed and stored as-is.
- A row with an empty `car_park_no` is skipped with a warning.

### 3.4 Ingestion timing

CSV parsing runs once at application startup before the HTTP server begins accepting requests. Startup fails if the file is missing.

---

## 4. SVY21 → WGS84 Conversion

### 4.1 Algorithm

Use the **standard Singapore SVY21 to WGS84** transformation parameters published by the Singapore Land Authority (SLA):

| Parameter | Value |
|-----------|-------|
| Semi-major axis (a) | 6 378 137.000 m |
| Inverse flattening (1/f) | 298.257 223 563 |
| Central meridian (λ₀) | 103°50′E |
| Latitude of origin (φ₀) | 1°22′N |
| False Easting (FE) | 28 001.642 m |
| False Northing (FN) | 38 744.572 m |
| Scale factor (k₀) | 1.0 |

Apply the standard Transverse Mercator inverse projection to obtain WGS84 latitude and longitude. No third-party geodesy library is required; the formula is well-defined and can be implemented in a small utility class.

### 4.2 Accuracy requirement

The conversion error must be ≤ 1 m, as stated in PRD section 8.

### 4.3 Location in code

`CarparkAvailability.ApiApp` — a static utility class `Svy21Converter` with a single public method:

```csharp
public static (double Latitude, double Longitude) Convert(double easting, double northing);
```

---

## 5. ApiApp — REST Endpoints

All endpoints return `application/json`. Errors return RFC 9457 Problem Details (provided by `app.UseExceptionHandler()` and `builder.Services.AddProblemDetails()`).

### 5.1 `GET /api/carparks`

Returns the merged dataset for all car parks that have both static HDB data and live availability.

**Query parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `lat` | double | Yes | WGS84 latitude of the destination. |
| `lng` | double | Yes | WGS84 longitude of the destination. |
| `radius` | int | No | Search radius in metres. Default and maximum: `500`. |

**Response body:**

```jsonc
{
  "timestamp": "2026-08-19T08:00:00+08:00",  // last successful API poll time (ISO 8601 SGT)
  "freshness": "live",                         // "live" | "stale" | "unavailable"
  "carparks": [
    {
      "carparkNo": "ACB",
      "address": "BLK 270/271 ALBERT CENTRE BASEMENT CAR PARK",
      "latitude": 1.301,
      "longitude": 103.847,
      "distanceMetres": 123,
      "carparkType": "BASEMENT CAR PARK",
      "typeOfParkingSystem": "ELECTRONIC PARKING",
      "shortTermParking": "WHOLE DAY",
      "freeParking": "NO",
      "nightParking": "YES",
      "carparkDecks": 1,
      "gantryHeight": 1.8,
      "carparkBasement": true,
      "lots": [
        {
          "lotType": "C",
          "totalLots": 500,
          "lotsAvailable": 123
        }
      ],
      "updateDatetime": "2026-08-19T07:59:00+08:00"
    }
  ]
}
```

**Freshness logic:**

- `"live"` — `timestamp` is ≤ 5 minutes before the server's current SST clock.
- `"stale"` — `timestamp` is > 5 minutes old but a previous poll succeeded.
- `"unavailable"` — no successful poll has completed yet in this server process.

**Sorting:** Results are sorted by `distanceMetres` ascending before serialisation.

**Error responses:**

| Condition | HTTP status |
|-----------|-------------|
| `lat` or `lng` missing | 400 Bad Request |
| `lat` / `lng` outside Singapore bounds (`lat` 1.15–1.48, `lng` 103.58–104.09) | 400 Bad Request |
| Unexpected server error | 500 Internal Server Error |

### 5.2 `GET /api/carparks/{carparkNo}`

Returns the detail record for a single car park.

**Path parameter:** `carparkNo` — the HDB car park identifier (e.g., `ACB`).

**Response body:** single object from the `carparks` array schema above.

**Error responses:**

| Condition | HTTP status |
|-----------|-------------|
| Car park not found in HDB dataset | 404 Not Found |

### 5.3 `GET /api` (scaffold endpoint)

Returns a health-check summary. Already present in the scaffold; must not be removed.

---

## 6. data.gov.sg API Integration

### 6.1 Configuration

- Base URL: `https://api.data.gov.sg/v1`
- Endpoint: `GET /transport/carpark-availability`
- API key header: `x-api-key` — value from `DataGovSg__ApiKey` environment variable.
- The API key is optional (unauthenticated requests are allowed but rate-limited); the application must function without it if the environment variable is absent.

### 6.2 Polling

- A hosted `IHostedService` (background service) polls the endpoint every **60 seconds**.
- The first poll occurs on service startup before the HTTP server starts serving `/api/carparks` (use `IHostedService.StartAsync` to run the first poll synchronously).
- The HTTP client is registered via `IHttpClientFactory`; resilience defaults from ServiceDefaults apply.

### 6.3 Response parsing

The API returns a JSON object with this top-level shape:

```jsonc
{
  "api_info": { "status": "healthy" },
  "items": [
    {
      "timestamp": "<ISO 8601 SGT>",
      "carpark_data": [
        {
          "carpark_number": "ACB",
          "carpark_info": [
            {
              "total_lots": "500",       // string; must be parsed to int
              "lot_type": "C",
              "lots_available": "123"    // string; must be parsed to int
            }
          ],
          "update_datetime": "<ISO 8601 SGT>"
        }
      ]
    }
  ]
}
```

- `total_lots` and `lots_available` are strings; parse to `int`. On parse failure, default to `0` and log a warning.
- Use `items[0].carpark_data` from the response (the API returns a single-element `items` array).
- The `update_datetime` on each `carpark_data` entry is the authoritative per-car-park freshness timestamp.

### 6.4 Last-known-good behaviour

- On successful poll: update the in-memory store and record the poll timestamp.
- On failed poll (network error or non-2xx response): log the error, retain the existing in-memory store unchanged, and do **not** update the poll timestamp.
- The last successful poll timestamp is returned in the `GET /api/carparks` response as `timestamp`.

---

## 7. Join Logic

After each successful poll, the background service merges live data with the HDB static dataset:

1. For each `carpark_data` entry, look up `car_park_no = carpark_number` in the HDB dictionary (case-insensitive comparison after trimming).
2. If a match is found: create a merged record combining all HDB fields, all `carpark_info` lot entries, and the `update_datetime`.
3. If no HDB match: the car park is included in the merged store without static details (address and type fields are empty/null) — it is excluded from API responses that require address display.
4. If an HDB entry has no matching real-time record: the car park is still available in the detail endpoint but its `lots` array is empty and freshness is `"unavailable"`.

---

## 8. Distance Calculation

Use the **Haversine formula** with Earth radius **6 371 000 m**.

```csharp
double DistanceMetres(double lat1, double lon1, double lat2, double lon2);
```

Applied in ApiApp when handling `GET /api/carparks` to filter and sort results. Calculation runs in memory on the merged dataset; no spatial database is required.

---

## 9. WebApp — Google Maps Integration

### 9.1 API key injection

`GoogleMaps__ApiKey` is injected as an environment variable by AppHost. WebApp reads it via `IConfiguration["GoogleMaps__ApiKey"]` and renders it into the Blazor page as a JavaScript variable or Maps SDK `<script>` tag parameter.

The key must **not** appear in any server-side API response or log entry.

### 9.2 Maps JavaScript SDK

- Load dynamically via a `<script>` tag pointing to `https://maps.googleapis.com/maps/api/js?key={apiKey}&libraries=places`.
- Initialise the map centred on Singapore: `{ lat: 1.3521, lng: 103.8198, zoom: 12 }`.
- Add markers for each car park returned by `GET /api/carparks`.
- Clicking a marker selects the corresponding car park and opens the detail panel.

### 9.3 Places autocomplete

- Use `google.maps.places.Autocomplete` bound to the search input.
- Restrict results to Singapore: `componentRestrictions: { country: 'sg' }`.
- On place selection, extract `geometry.location` (lat/lng) and call `GET /api/carparks?lat=…&lng=…`.

### 9.4 No client-side data.gov.sg calls

WebApp must not call `api.data.gov.sg` from any Blazor component, JavaScript module, or browser-side fetch.

---

## 10. Error Handling

### 10.1 ApiApp

| Scenario | Behaviour |
|----------|-----------|
| data.gov.sg unreachable | Log warning; return last-known-good data with `"freshness": "stale"` or `"unavailable"`. |
| data.gov.sg returns non-2xx | Same as unreachable; log response status code. |
| CSV file missing at startup | Log fatal error; application exits with non-zero exit code. |
| CSV row parse error | Log warning; skip row; continue startup. |
| SVY21 conversion of out-of-range coordinates | Log warning; skip the car park entry. |
| Missing required query parameters | Return 400 Problem Details. |
| Unhandled exception | Return 500 Problem Details (via `UseExceptionHandler`). |

### 10.2 WebApp

| Scenario | Behaviour |
|----------|-----------|
| ApiApp unreachable | Show non-blocking error banner: "Parking data is temporarily unavailable." |
| ApiApp returns non-2xx | Same as unreachable; include HTTP status in the logged error. |
| Google Maps SDK fails to load | Show static message: "Map unavailable — check your connection." |
| Empty search result | Show empty-state message per PRD section 5.7. |
| Geocoding failure | Show inline error in the search box per PRD section 5.7. |

---

## 11. Security

| Requirement | Implementation |
|-------------|----------------|
| data.gov.sg API key confined to ApiApp | Key read from `DataGovSg__ApiKey` env var; never serialised or logged. |
| Google Maps API key confined to WebApp | Key read from `GoogleMaps__ApiKey` env var; rendered only into the Maps SDK `<script>` tag. |
| No secrets in source control | Both keys are stored in user secrets (`dotnet user-secrets`) or environment variables only. |
| API key optional | Application starts and serves cached CSV data even without a data.gov.sg key. |
| No CORS configuration required | WebApp and ApiApp communicate server-to-server via Aspire service discovery; no cross-origin browser requests. |
| Input validation | `lat`/`lng` query parameters validated to Singapore bounds before processing. |

---

## 12. Accessibility

- Freshness badges must include visible text (not colour alone): "Live", "Stale", "Unavailable".
- All interactive Blazor components must have appropriate ARIA labels.
- Map markers must have `title` attributes containing the car park number and address.

---

## 13. Testing

### 13.1 Test project location

All tests go in `tests/` at the repository root. Follow `dotnet test CarparkAvailability.slnx` once test projects are present.

### 13.2 Required test coverage

| Area | Test type | Acceptance criteria |
|------|-----------|---------------------|
| SVY21 → WGS84 conversion | Unit | Known reference coordinates from SLA documentation convert within 1 m error. |
| CSV parsing | Unit | Valid rows parsed correctly; invalid rows skipped with warnings; missing file throws. |
| Haversine distance | Unit | Reference distances match expected values within 0.5 m. |
| Join logic | Unit | Matched, unmatched API, and unmatched HDB entries behave per section 7. |
| Freshness logic | Unit | "live" / "stale" / "unavailable" states computed correctly against mocked clocks. |
| `GET /api/carparks` | Integration | Returns 400 for missing params, 400 for out-of-bounds coords, 200 with correct sorting. |
| `GET /api/carparks/{carparkNo}` | Integration | Returns 200 with full detail; returns 404 for unknown car park. |
| Filter logic | Unit | Each filter from PRD section 5.4 applied to a sample dataset returns the correct subset. |
| data.gov.sg poll failure | Integration | Last-known-good data is returned; poll timestamp is not updated. |

### 13.3 Contract tests

- Validate a representative live data.gov.sg response against the schema in section 6.3.
- Accept backward-compatible additions (unknown optional fields).
- Fail on missing required fields (`carpark_number`, `carpark_info`, `total_lots`, `lot_type`, `lots_available`, `update_datetime`).
- Contract tests are tagged `[Trait("Category", "Contract")]` and skipped in CI unless a live API key is present.

---

## 14. Out of Scope

The following are **explicitly excluded** from this TRD (mirroring PRD section 6):

- Relational or document databases
- Redis or distributed caching
- Authentication and authorisation
- Deployment manifests (Docker, Kubernetes, Azure)
- MCP server or tool definitions
- Agentic AI components (deferred to step 05)
- Occupancy forecasting or historical data storage
- Push notifications or SignalR alerts
- Traffic or weather API integrations
- Vehicle profile or user preferences storage

---

## 15. Glossary

| Term | Definition |
|------|------------|
| SVY21 | Singapore Transverse Mercator coordinate system used in HDB datasets. |
| WGS84 | World Geodetic System 1984; latitude/longitude used by Google Maps. |
| SST | Singapore Standard Time — UTC+8; no daylight-saving adjustment. |
| Haversine | Great-circle distance formula used for surface distance between two WGS84 points. |
| Last-known-good | The most recent successfully parsed API response retained in memory after a poll failure. |
| Lot type C/H/S/Y | Car / Heavy vehicle / Motorcycle with sidecar / Motorcycle (as defined by data.gov.sg). |
