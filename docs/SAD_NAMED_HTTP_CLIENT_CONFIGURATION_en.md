# Named HttpClient configuration

## Summary

`ProductService` no longer uses a hardcoded absolute URL. The Product API base address comes from configuration, and the HTTP call uses the named `ProductApi` client.

## Implemented Changes

- Added `ApiPaths:ProductApiBaseAddress` to `appsettings.Development.json`.
- Added the central `HttpClientNames.ProductApi` name.
- Registered the named client with the existing correlation handler.
- Updated `ProductService` to use `IHttpClientFactory` and the relative `users` path.
- Added focused tests for named client selection, request URI composition, and Autofac resolution.

## Affected Components

- `Template.Service.Api` - client configuration and registration.
- `Template.Service.Services.Interfaces` - central client name.
- `Template.Service.Services` - named client consumption.
- `Template.Service.Api.Tests` - focused test coverage.

## Configuration / Deployment Impact

Each environment must provide `ApiPaths:ProductApiBaseAddress`. The development value is included in `appsettings.Development.json`.

## Validation

- Solution build: passed with 0 errors and 2 pre-existing nullable warnings.
- Tests: 23/23 passed.

## Further Improvements

- Add separate named clients, addresses, and resilience policies as new downstream APIs are introduced.

## Change Log - 2026-08-19

- Added the `HRApi` named client alongside the existing `ProductApi` client.
- Moved individual client registration from `Program.cs` into the `Template.Service.DependencyInjection` composition root.
- `Program.cs` uses one delegating call to register all service HTTP clients.
- `ProductService` creates both required clients in its constructor when DI resolves the service; clients are not created inside individual methods.
- Both clients use the correlation handler.
- The build passes and all tests pass (26/26).

