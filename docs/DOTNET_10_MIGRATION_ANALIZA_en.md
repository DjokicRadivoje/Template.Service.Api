# Template.Service.Api Solution Migration to .NET 10 - Analysis

## Status

- Phase: implementation completed and locally validated; commit and CI validation are pending.
- Analysis date: August 20, 2026.
- Branch during analysis: `main`.
- Dependency: this work is a prerequisite for the server-side and client-side JWT implementations.
- Reference document: `E:\GIT_2026\rafajlovski-chatbot_2026\keycloakJWT.md`.

## Goal

Migrate all 12 projects in `Template.Service.Api.sln` from `net8.0` to `net10.0`, align framework-dependent packages, and confirm that existing behavior remains unchanged before JWT and CRM functionality is introduced.

The migration is a separate technical unit. Authentication, authorization, Keycloak configuration, the CRM client, and new business methods are not added in this part.

## Current State

- All 12 `.csproj` files explicitly target `net8.0`.
- .NET SDK `10.0.400` is installed on the development machine.
- There is no root `global.json`, so SDK selection depends on the execution environment.
- Explicit `Microsoft.Extensions.*` references use 8.x versions.
- `Serilog.AspNetCore` uses an 8.x version.
- The solution uses Autofac, Serilog, Swashbuckle, AutoMapper, and xUnit packages that must be verified on .NET 10.
- The worktree has no tracked-file changes; two previously existing untracked GitHub Actions YML files are outside this migration scope.

## Expected State

- Every project targets `net10.0`.
- A root `global.json` selects the approved .NET 10 SDK and permits only the agreed patch roll-forward behavior.
- Framework-dependent Microsoft references are aligned with 10.x.
- Third-party packages remain at their current versions when compatible and are changed only when required for .NET 10 compatibility, without unnecessary scope expansion.
- The solution restores, builds, and passes tests on .NET 10 without functional changes.
- The CI workflow uses the .NET 10 SDK before the migration is merged.

## Projects in Scope

1. `Framework.Logger`
2. `Framework.Logger.Tests`
3. `Template.Service.DependencyInjection`
4. `Template.Service.Api`
5. `Template.Service.Api.Tests`
6. `Template.Service.BusinessLogic`
7. `Template.Service.BusinessModel`
8. `Template.Service.DataModel`
9. `Template.Service.BusinessLogic.Interfaces`
10. `Template.Service.Services.Interfaces`
11. `Template.Service.Mapper`
12. `Template.Service.Services`

## Proposed Solution

### SDK pinning

Add `global.json` at the repository root. The initial value follows the installed SDK:

```json
{
  "sdk": {
    "version": "10.0.400",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  }
}
```

Before commit, confirm that CI and development environments can install or use this SDK line. If the organizational standard requires another .NET 10 feature band, align `global.json` with that standard.

### Target framework

Change the following in all 12 projects:

```xml
<TargetFramework>net8.0</TargetFramework>
```

to:

```xml
<TargetFramework>net10.0</TargetFramework>
```

Do not introduce multi-targeting unless a concrete consumer still requires .NET 8.

### Packages

- Align explicit `Microsoft.Extensions.Configuration.Abstractions`, `Microsoft.Extensions.Http`, and `Microsoft.Extensions.Logging.Abstractions` references with 10.x.
- Align `Serilog.AspNetCore` with an ASP.NET Core 10-compatible version.
- Verify, but do not automatically change, Autofac, Autofac.Extensions.DependencyInjection, Swashbuckle, AutoMapper, and test packages.
- Do not add `Microsoft.AspNetCore.Authentication.JwtBearer` in this phase; it belongs to the server-side JWT analysis.
- Do not introduce central package management as part of this migration because that would be a separate refactor.

## Implementation Flow

```text
Baseline build/test on net8.0
  -> add global.json
  -> change every TargetFramework value
  -> align Microsoft/ASP.NET packages
  -> restore
  -> build the full solution
  -> run all tests
  -> review warnings and perform runtime startup verification
  -> start JWT work only after that
```

## Affected Files

- `global.json` - new SDK pinning document.
- all 12 `.csproj` files - target framework and required package alignment.
- `.github/workflows/*.yml` - only if an existing workflow explicitly installs an older SDK; the two currently untracked files must not be changed without a separate ownership and purpose review.
- migration documentation and a later implementation log.

## Risks

- A third-party package can restore successfully but still have a runtime issue on .NET 10.
- An ASP.NET Core or test-host change can expose previously hidden warnings or behavior.
- Pinning a feature band unavailable in CI can block the build.
- Combining the migration with JWT changes would make migration and functional failures harder to distinguish.
- Inconsistent target frameworks can cause unnecessary compatibility asset selection.

## Open Questions

- Which exact .NET 10 SDK feature band does CI use: `10.0.4xx` or another organization-approved version?
- Are the existing GitHub Actions files adopted as part of this migration, or do they remain outside the scope?
- Does the organization require a separate container/base-image update?

These questions do not block the local start of the migration but must be resolved before CI/deployment validation.

## Validation Plan

1. Record the existing build and test results before changes.
2. Run `dotnet --version` and confirm the SDK selected by `global.json`.
3. Run `dotnet restore Template.Service.Api.sln`.
4. Run `dotnet build Template.Service.Api.sln --no-restore`.
5. Run `dotnet test Template.Service.Api.sln --no-build`, or run both test projects separately.
6. Start `Template.Service.Api` with the existing development configuration and verify startup.
7. Compare the number and type of warnings with the baseline.
8. Confirm that no unintended functional or configuration changes were introduced.

## Acceptance Criteria

- [x] All 12 projects target `net10.0`.
- [x] `global.json` selects the locally approved .NET 10 SDK `10.0.400`.
- [x] Restore passes.
- [x] The solution builds without errors.
- [x] All existing tests pass, 26/26.
- [x] The API starts in Development mode; the Swagger startup probe returns HTTP 200.
- [x] No JWT/CRM functional changes are included in the migration part.
- [ ] CI is ready to use .NET 10.

## Implementation Result - August 20, 2026

- The baseline `net8.0` build passes with 4 existing nullable warnings.
- Baseline tests pass, 26/26.
- A root `global.json` was added with SDK `10.0.400` and the `latestPatch` rule.
- All 12 projects were migrated to `net10.0`.
- Explicit `Microsoft.Extensions.*` references were aligned to 10.0.11.
- `Serilog.AspNetCore` was aligned to 10.0.0, `Serilog` to 4.3.0, and `Serilog.Sinks.File` to 7.0.0.
- .NET 10 restore and build pass; the build reports 4 existing nullable warnings and 5 propagated NU1903 warnings for vulnerable AutoMapper 12.0.1.
- .NET 10 tests pass, 26/26.
- The Development startup probe passes with HTTP 200 on the Swagger page.
- A build without explicit `-c Debug` fails because of an external/pre-existing MSBuild value, `configuration-service/|Any CPU`; validation therefore used explicit `-c Debug`.
- The AutoMapper major migration was not included because the deprecated DI package and licensing/major API implications require a separate decision. The vulnerability remains an open high-severity follow-up.
- CI workflow files were not changed because the two `.github/workflows` files were pre-existing user-owned untracked files and were outside the approved scope.

## Recommended Commit Scope

```text
chore: migrate Template.Service.Api solution to .NET 10
```

A commit is created only upon the user's explicit request.

