# Template.Service.Api Solution Migration to .NET 10

## Summary

All 12 projects in `Template.Service.Api.sln` were migrated from .NET 8 to .NET 10 without changing business behavior.

## Implemented Changes

- Added `global.json` for SDK `10.0.400` with the `latestPatch` rule.
- All 12 projects now target `net10.0`.
- Explicit Microsoft.Extensions packages were aligned to 10.0.11.
- The Serilog ASP.NET Core package and its direct dependencies were aligned with the .NET 10 line.

## Affected Components

- `global.json` - SDK selection.
- all `.csproj` files - target framework.
- `Template.Service.DependencyInjection`, `Template.Service.BusinessLogic`, `Template.Service.Services` - Microsoft.Extensions packages.
- `Template.Service.Api` - Serilog packages.

## Configuration / Deployment Impact

CI, runtime, and container environments must provide a compatible .NET 10 SDK/runtime. Application configuration keys were not changed.

## Validation

- Baseline .NET 8 build: passed with 4 existing warnings.
- Baseline tests: 26/26 passed.
- .NET 10 restore/build: passed with 0 errors.
- .NET 10 tests: 26/26 passed.
- Development Swagger startup probe: HTTP 200.
- Open: AutoMapper 12.0.1 high-severity `NU1903`; a separate security follow-up is required.

## Further Improvements

- Validate the CI/container image on .NET 10.
- Resolve the AutoMapper vulnerability in a separate task.
- Investigate the external MSBuild `Configuration` value that requires explicit `-c Debug`.

