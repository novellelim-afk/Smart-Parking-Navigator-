# Product Requirements Document — Smart Parking Navigator

| Field        | Value                                                       |
|--------------|-------------------------------------------------------------|
| Status       | Draft                                                       |
| Version      | 1.0.0                                                       |
| Date         | 2026-08-19                                                  |
| Author       | Copilot Coding Agent                                        |
| Reviewers    | Workshop facilitators                                       |
| Issue        | [#2 Generate PRD.md and TRD.md](../../issues/2)             |
| Scope        | MVP — workshop deliverable, no deployment, no persistence   |

---

## 1. Purpose

Smart Parking Navigator helps drivers in Singapore find available HDB car parks near a destination. It combines the static HDB Carpark Information dataset with the live data.gov.sg Car Park Availability API to show real-time lot counts, operating conditions, and freshness signals on a Google Maps interface.

---

## 2. Target Users

| Persona | Description |
|---------|-------------|
| **Daily commuter** | Drives to MRT stations or town centres; needs a fast answer on where to park before arriving. |
| **Occasional driver** | Unfamiliar with parking in an area; needs both location and condition details. |
| **Heavy-vehicle operator** | Needs height clearance and lot-type information before committing to a route. |

All personas operate within Singapore and primarily use the application on a desktop or tablet browser. Mobile responsiveness is desirable but not P0 for the MVP.

---

## 3. Singapore Context

- HDB manages approximately 2 000 public car parks across Singapore's towns.
- Real-time availability data is published every minute through the data.gov.sg Car Park Availability API (`GET /v1/transport/carpark-availability`).
- Lot types reported by the API: **C** (cars), **H** (heavy vehicles), **S** (motorcycles with sidecar), **Y** (motorcycles).
- Static carpark attributes come from the HDB Carpark Information CSV dataset.
- Coordinates in the HDB dataset use the SVY21 projection and must be converted to WGS84 before map display.
- All times are Singapore Standard Time (SST, UTC+8).

---

## 4. User Journeys

### 4.1 Find parking near a destination

1. User opens the application; a full-screen Google Map of Singapore is displayed.
2. User types a destination address or place name into the search box.
3. The map pans and zooms to the destination; a marker is placed at the searched location.
4. Car parks within 500 m of the destination are listed and pinned on the map.
5. Each listing shows: distance, available lots (car type), total lots, occupancy percentage, and data freshness.
6. User selects a car park to view its full details panel.

### 4.2 Filter results

1. From the results list the user opens the filter panel.
2. User applies one or more filters (available-only, vehicle type, night parking, car-park type).
3. The map and list update in place to show only matching car parks.

### 4.3 Handle stale or unavailable data

1. If the most recent successful API response is older than 5 minutes, each affected car-park listing shows a **Stale** badge with the last-known update time.
2. If the API is unreachable, a non-blocking banner informs the user that availability data is unavailable and shows the last-known update time.
3. Static car-park details (address, type, conditions) remain visible at all times.

---

## 5. MVP Feature Scope (P0)

All features in this section are required for the MVP.

### 5.1 Destination search

- Free-text search restricted to Singapore geography.
- Powered by the Google Maps Places API (JavaScript SDK in WebApp).
- On selection the map centres on the destination and a marker is placed.

### 5.2 Nearby car parks

- Retrieve all HDB car parks whose WGS84 coordinates fall within **500 m** of the destination (straight-line Haversine distance).
- Display up to **20** results sorted by ascending distance.
- Each result card shows:
  - Car park number and address
  - Distance in metres
  - Available lots / total lots for each reported lot type
  - Occupancy percentage (derived from car-type lots; displayed as a coloured bar)
  - Car park type (Surface / Multi-storey / Basement / Mechanised)
  - Freshness indicator (live / stale / unavailable)

### 5.3 Live lot availability

- Poll `GET /v1/transport/carpark-availability` every **60 seconds**.
- Parse `carpark_data[].lot_type`, `carpark_data[].lots_available`, and `carpark_data[].total_lots`.
- Use `update_datetime` from the API response as the authoritative freshness timestamp.
- On a poll failure, retain the last successful response and show its timestamp.

### 5.4 Filters

| Filter | Values |
|--------|--------|
| Available only | Toggle: show only car parks with `lots_available > 0` for the selected lot type |
| Lot type | Multi-select: C, H, Y (S excluded from UI as it is rarely reported) |
| Night parking | Toggle: `night_parking = YES` |
| Car park type | Multi-select: Surface, Multi-storey, Basement, Mechanised |

Filters apply to both the list and the map pins; no server round-trip is required.

### 5.5 Car park detail panel

Displayed when a car park is selected from the list or map:

| Field | Source |
|-------|--------|
| Car park number | HDB CSV |
| Address | HDB CSV |
| Car park type | HDB CSV `car_park_type` |
| Parking system | HDB CSV `type_of_parking_system` |
| Short-term parking hours | HDB CSV `short_term_parking` (displayed as-is) |
| Free parking conditions | HDB CSV `free_parking` (displayed as-is; no time interpretation) |
| Night parking | HDB CSV `night_parking` |
| Decks | HDB CSV `car_park_decks` |
| Gantry height | HDB CSV `gantry_height` (metres; hidden when 0) |
| Basement | HDB CSV `car_park_basement` |
| Available lots | data.gov.sg API, per lot type |
| Total lots | data.gov.sg API, per lot type |
| Last updated | `update_datetime` from API |
| Freshness | Live / Stale / Unavailable |

### 5.6 Freshness states

| State | Condition | Display |
|-------|-----------|---------|
| **Live** | Last update ≤ 5 min ago | Green badge |
| **Stale** | Last update > 5 min ago | Amber badge with timestamp |
| **Unavailable** | No successful poll yet in this session | Grey badge; static details still shown |
| **Error** | API returned a non-2xx response | Red banner (non-blocking) |

### 5.7 Loading and empty states

- While the first API poll is in progress: skeleton loaders replace availability figures.
- If no car parks are found within 500 m: "No HDB car parks found within 500 m" message.
- If the search geocoding fails: "Location not found" message in the search box.

---

## 6. Out of Scope for MVP

The following are **explicitly deferred**:

- Favourites, saved searches, and user accounts
- Push or in-app availability alerts
- Occupancy forecasting and historical data storage
- Traffic and weather integration
- Vehicle profile (automatic height / type filtering)
- Paid parking or rate information
- Operations or admin dashboards
- Deployment to any cloud or container platform
- Relational or document databases
- MCP (Model Context Protocol) integration
- Agentic AI features (deferred to step 05 capstone)

---

## 7. Measurable Acceptance Criteria

### AC-01 — Destination search

- Given the user types a valid Singapore address, the map moves to that location within 3 seconds.
- Given the user types a non-Singapore address, the Places autocomplete returns no results or the map does not leave Singapore bounds.

### AC-02 — Nearby car parks

- Given a destination with at least one HDB car park within 500 m, the list shows ≥ 1 car park.
- All displayed car parks are ≤ 500 m from the destination (Haversine).
- Results are sorted by ascending distance.
- No more than 20 results are shown.

### AC-03 — Availability data

- Available and total lots match the most recent successful API poll.
- Lot types C, H, and Y are distinguished in the UI where data is present.

### AC-04 — Filters

- Enabling "Available only" removes all car parks with `lots_available = 0` for cars from the list and map.
- Each filter combination produces a consistent result between the list and the map.

### AC-05 — Detail panel

- All P0 fields from section 5.5 are visible in the detail panel.
- Gantry height is hidden when the CSV value is `0`.

### AC-06 — Freshness

- A car park whose `update_datetime` is > 5 minutes old displays the **Stale** badge.
- When the API is unreachable the **Unavailable** badge is shown and the last-known update time is displayed.

### AC-07 — Polling

- A new availability request is made every 60 seconds without a page reload.
- A failed poll does not clear previously loaded availability data.

### AC-08 — Security

- The data.gov.sg API key is never present in any browser-visible asset or HTTP response.
- The Google Maps API key is never present in any server-side API response.

---

## 8. Non-Functional Requirements

| Category | Requirement |
|----------|-------------|
| Availability | Application starts and serves requests on a developer workstation via `dotnet run`. |
| Performance | Car-park list renders within 2 s of destination selection on a local network. |
| Accessibility | Colour is not the sole differentiator for freshness states (badge text required). |
| Browser support | Latest stable versions of Chrome and Edge. |
| Data accuracy | SVY21→WGS84 conversion error ≤ 1 m. |
