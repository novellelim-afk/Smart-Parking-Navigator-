# Repository Instructions for Coding Agents

This file describes the Smart Parking Navigator repository so that coding agents can work safely and consistently without inventing architecture or commands that are not present.

## Project Overview

Smart Parking Navigator is a workshop application for finding suitable HDB car parks in Singapore. It is built on .NET 10 Aspire and consists of four projects under `src/`. Application behaviour (parking features, internal API contract, tests) has **not yet been implemented**; this is a prepared starter scaffold.

## Solution Structure

```text
/
├── data/
│   ├── carpark-availability.http       # Sample HTTP request against data.gov.sg
│   ├── CarparkAvailability.json        # Representative car-park availability data
│   └── HDBCarparkInformation.csv       # HDB car park information dataset
├── docs/                               # Workshop step guides
├── src/
│   ├── CarparkAvailability.ApiApp/     # ASP.NET Core minimal-API backend
│   ├── CarparkAvailability.AppHost/    # .NET Aspire orchestration host
│   ├── CarparkAvailability.ServiceDefaults/  # Shared observability defaults
│   └── CarparkAvailability.WebApp/     # Blazor Server frontend
├── CarparkAvailability.slnx            # Solution file
├── Directory.Build.props
├── Directory.Packages.props            # Central package version management
└── global.json                         # .NET SDK pin (10.0.100, latestFeature)
```

### Project Responsibilities

| Project | Role |
|---|---|
| **ApiApp** | ASP.NET Core minimal-API service. Calls data.gov.sg server-side and combines live availability with the HDB dataset. Exposes JSON endpoints consumed by WebApp. |
| **WebApp** | Blazor Server frontend. Calls ApiApp via service discovery. Renders Google Maps in the browser. Never calls data.gov.sg directly. |
| **AppHost** | .NET Aspire host that orchestrates ApiApp and WebApp, injects secrets as environment variables, and exposes the Aspire dashboard. |
| **ServiceDefaults** | Shared extension methods that configure OpenTelemetry, health checks, and service discovery for all projects. |

## Technology Stack

- **.NET 10** (`global.json` pins SDK `10.0.100`, `rollForward: latestFeature`)
- **.NET Aspire** — distributed application orchestration, dashboard, service discovery
- **ASP.NET Core** — minimal-API pattern in ApiApp
- **Blazor Server** — interactive server-side rendering in WebApp
- **Central Package Management** — all `PackageVersion` entries live in `Directory.Packages.props`; individual `.csproj` files must not repeat version numbers

## Commands

All commands are run from the repository root unless stated otherwise.

### Restore and build

```bash
dotnet restore CarparkAvailability.slnx
dotnet build CarparkAvailability.slnx
```

### Run the application

```bash
dotnet run --project src/CarparkAvailability.AppHost
```

The Aspire dashboard URL is printed to the terminal after startup.

### Validate a single project

```bash
dotnet build src/CarparkAvailability.ApiApp
dotnet build src/CarparkAvailability.WebApp
```

### Tests

No test projects exist yet. When tests are added they must follow the eventual PRD and TRD and be placed in a `tests/` directory at repository root. Use `dotnet test CarparkAvailability.slnx` once test projects are present.

## Data and API Contract

- `data/HDBCarparkInformation.csv` — static HDB car park dataset; read at startup or build time.
- `data/CarparkAvailability.json` — representative snapshot of the data.gov.sg Car Park Availability API response; use as a reference for the API contract and for local development.
- `data/carpark-availability.http` — sample HTTP request that shows the actual data.gov.sg endpoint and required headers.

## Service Boundaries and Secrets

- **data.gov.sg must only be called from ApiApp** (server-side). Never call it from WebApp or any browser-side code.
- **Google Maps must only be called from WebApp** (browser-side). The API key is injected into WebApp via the `GoogleMaps__ApiKey` environment variable set by AppHost.
- Secrets (`DataGovSg:ApiKey`, `GoogleMaps:ApiKey`) are stored in user secrets or environment variables and read by `AppHost` via `builder.AddParameterFromConfiguration`. See `src/CarparkAvailability.AppHost/AppHost.cs` for the exact parameter names.
- **Never commit credentials, API keys, or tokens** to source control.
- **Never hard-code API keys** in any source file. Always read them from configuration or environment.
- See `docs/data-gov-sg-api-key.md` and `docs/google-maps-api-key.md` for setup instructions.

## General Guidelines

- Do not invent projects, namespaces, endpoints, or configuration keys that are not present in the repository.
- Do not implement application features beyond what is explicitly requested in the issue or PRD/TRD.
- All new packages must be added to `Directory.Packages.props` with a version; the `.csproj` must reference them without a version.
- Follow existing code style: file-scoped namespaces, top-level statements in `Program.cs`, and `var` for local variables where the type is obvious.
- Generated or scaffolded files (e.g., Aspire manifest) must not be edited manually.

## Documentation

- New features must be documented in line with the eventual PRD (`PRD.md`) and TRD (`TRD.md`) once those documents exist.
- Update `README.md` only when the high-level curriculum or starter structure changes.
- Do not modify workshop guide files under `docs/` unless the issue explicitly targets them.

## Security

- Never expose secrets via logging or API responses.
- Keep CORS, authentication, and authorisation decisions consistent with the TRD once it exists.
- Review `SECURITY.md` for the project's vulnerability-disclosure policy.

## Commits and Pull Requests

- Use the imperative mood in commit subjects: `Add carpark availability endpoint`, not `Added` or `Adding`.
- Keep commits focused; one logical change per commit.
- Reference the relevant issue number in the PR description: `Closes #N`.
- Do not merge a PR until `dotnet build CarparkAvailability.slnx` succeeds.
- PR titles should match the issue title where possible.
