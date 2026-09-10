# Template.Service.Api Solution Migration to .NET 10 - Implementation Log

## Context

- Topic: .NET 10 migration
- Branch: `main`
- Date: August 20, 2026
- Analysis: `docs/DOTNET_10_MIGRATION_ANALIZA_en.md`
- Status: implemented and locally validated; commit and CI are pending.

## Timeline

- 2026-08-20 - Confirmed that all 12 projects target `net8.0` and SDK `10.0.400` is installed.
- 2026-08-20 - Baseline build without `-c` failed before compilation because of the invalid external MSBuild configuration `configuration-service/|Any CPU`.
- 2026-08-20 - Baseline build with `-c Debug` passed with 4 existing nullable warnings.
- 2026-08-20 - Baseline tests passed, 26/26.
- 2026-08-20 - Added `global.json`, migrated all 12 projects to `net10.0`, and aligned framework-dependent packages.
- 2026-08-20 - The first restore in the sandbox failed because network access was blocked; the repeated approved NuGet restore passed.
- 2026-08-20 - Restore reported high-severity `NU1903` for existing AutoMapper 12.0.1.
- 2026-08-20 - The .NET 10 build passed with 0 errors and 9 warnings.
- 2026-08-20 - .NET 10 tests passed, 26/26.
- 2026-08-20 - The Development startup/Swagger probe returned HTTP 200.

## Implementation Notes

- `global.json` - pinned SDK `10.0.400`, `latestPatch`, no prerelease versions.
- all 12 `.csproj` files - changed `net8.0` to `net10.0`.
- `Template.Service.DependencyInjection.csproj` - aligned Microsoft Configuration/Http references to 10.0.11.
- `Template.Service.BusinessLogic.csproj` - aligned Logging.Abstractions to 10.0.11.
- `Template.Service.Services.csproj` - aligned Http and Logging.Abstractions to 10.0.11.
- `Template.Service.Api.csproj` - aligned the Serilog ASP.NET Core line with .NET 10.
- No JWT, CRM, or business behavior was introduced.

## Problems / Bugs

### External MSBuild configuration

- Symptom: `dotnet build Template.Service.Api.sln` uses `configuration-service/|Any CPU` and fails with MSB4126.
- Cause: external/pre-existing `Configuration` value, not introduced by the migration.
- Workaround: explicit `-c Debug`.
- Status: open environment follow-up; explicit Debug builds are unaffected.

### AutoMapper high-severity warning

- Symptom: five projects report `NU1903`, `GHSA-rvv3-g6hj-g44x`.
- Cause: existing deprecated `AutoMapper.Extensions.Microsoft.DependencyInjection` 12.0.1 brings AutoMapper 12.0.1.
- Decision: do not perform a major/licensing migration in the clean .NET 10 scope.
- Status: open high-severity follow-up; a separate AutoMapper removal or patched-version migration is required.

## Validation Log

- `dotnet build Template.Service.Api.sln -c Debug` on the net8 baseline - passed, 4 warnings, 0 errors.
- `dotnet test Template.Service.Api.sln -c Debug --no-build` on the net8 baseline - passed, 26/26.
- `dotnet restore Template.Service.Api.sln -p:Configuration=Debug` - passed after approved network access.
- `dotnet build Template.Service.Api.sln -c Debug --no-restore` on net10 - passed, 9 warnings, 0 errors.
- `dotnet test Template.Service.Api.sln -c Debug --no-build` on net10 - passed, 26/26.
- Development startup probe at `http://127.0.0.1:5059/swagger/index.html` - HTTP 200; the process was stopped after verification.

## Commits

- Pending commit.

## Deployment / Environment

- Dev: locally validated with SDK `10.0.400`.
- CI: not validated; the pre-existing untracked workflow files were not changed.
- Prod: not deployed.
- Config: no application configuration changes.

## Follow-up

- Resolve the AutoMapper high-severity vulnerability in a separate security task.
- Confirm the .NET 10 SDK feature band in CI and the runtime/container image.
- Investigate the source of the external `Configuration=configuration-service/` value.

