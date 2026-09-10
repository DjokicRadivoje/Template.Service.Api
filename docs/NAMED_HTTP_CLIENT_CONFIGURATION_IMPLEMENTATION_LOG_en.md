# Named HttpClient Configuration - Implementation Log

## Context

- Ticket: Not provided
- Branch: `main`
- Date: 2026-08-18
- Main documents: `docs/NAMED_HTTP_CLIENT_CONFIGURATION_ANALIZA_I_DOGOVOR.md` and `docs/NAMED_HTTP_CLIENT_CONFIGURATION_ANALIZA_en.md`

## Implemented Changes

- Added `ApiPaths:ProductApiBaseAddress` to the development configuration.
- Added the centralized client name `HttpClientNames.ProductApi`.
- Added base-address validation and named-client registration with the correlation handler.
- Updated `ProductService` to use `IHttpClientFactory`, the named client, and the relative `users` path.
- Added a direct `Microsoft.Extensions.Http` reference to `Template.Service.Services`.
- Added targeted `ProductService` and Autofac resolution tests.

## Validation Log

- `dotnet build Template.Service.Api.sln -c Debug` - passed, 0 errors; 2 pre-existing nullable warnings.
- `dotnet test Template.Service.Api.sln -c Debug --no-build --no-restore` - passed, 23/23.
- `git diff --check` - passed.

## Commits

- Pending commit.

## Deployment / Environment

- Development base address: `ApiPaths:ProductApiBaseAddress` in `appsettings.Development.json`.
- Other environments must provide the same configuration key.
- No database or migration changes.

## Follow-up

- Add separate named clients and configuration keys only when new downstream APIs are introduced.

## Multiple Client Composition Root Update - 2026-08-19

### Implemented Changes

- Removed `ProductApi` client registration from `Program.cs`.
- `Program.cs` now delegates client registration to `DependencyInjectionConfig.ConfigureHttpClients` through a single call.
- `Template.Service.DependencyInjection` registers `ProductApi` and `HRApi`, validates both base-address keys, and adds the correlation handler to both pipelines.
- `ProductService` retains constructor-based creation of both required clients; creation inside individual methods was not introduced.
- Tests verify both base addresses, Autofac resolution, both relative endpoints, and invalid-configuration validation.

### Validation

- `dotnet build Template.Service.Api.sln -c Debug` - passed, 0 errors; 4 pre-existing nullable warnings in DTO models.
- `dotnet test Template.Service.Api.Tests/Template.Service.Api.Tests.csproj -c Debug --no-build --no-restore` - passed, 14/14.
- `dotnet test Template.Service.Api.sln -c Debug --no-build --no-restore` - passed, 26/26.
- `git diff --check` - passed.

### Deployment / Environment

- Every environment must provide both `ApiPaths:ProductApiBaseAddress` and `ApiPaths:HRApiBaseAddress`.
- No database or migration changes.

