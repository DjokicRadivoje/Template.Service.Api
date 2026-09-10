# Service.Api.Template Keycloak JWT Server Protection - Implementation Log

## Context

- Topic: incoming Keycloak JWT access-token validation
- Branch: `main`
- Date: August 20, 2026
- Analysis: `docs/KEYCLOAK_JWT_SERVER_ANALIZA_en.md`
- Status: implemented and locally validated; commit, CI, and real Keycloak E2E are pending.

## Timeline

- Confirmed that `ProductController` had no authentication protection and that only `UseAuthorization()` existed.
- Added the .NET 10 JWT Bearer package and central `AddKeycloakAuthentication(...)` registration.
- Introduced fail-fast Authority, Audience, and clock-skew configuration validation.
- Introduced a global fallback policy protecting all current and future controller endpoints.
- Placed `UseAuthentication()` before `UseAuthorization()`.
- Added a Bearer/JWT security definition to Development Swagger.
- Added offline configuration and integration tests using locally signed tokens.
- Completed a successful build, 40 tests, and a short runtime probe.

## Implemented Changes

- `Template.Service.Api/Template.Service.Api.csproj`
  - added `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.11.
- `Template.Service.Api/Infrastructure/Authentication/KeycloakAuthenticationOptions.cs`
  - introduced `Authority`, `Audience`, `RoleClientId`, and `ClockSkewSeconds`.
- `Template.Service.Api/Infrastructure/Authentication/KeycloakAuthenticationExtensions.cs`
  - registered the default Bearer scheme;
  - enabled issuer, audience, lifetime, and signing-key validation;
  - set `MapInboundClaims = false`, HTTPS metadata, and explicit clock skew;
  - added a global `RequireAuthenticatedUser()` fallback policy;
  - added safe events that never log a token, header, or claim value.
- `Template.Service.Api/Program.cs`
  - invoked JWT registration;
  - added the Swagger Bearer scheme;
  - added `UseAuthentication()` before authorization;
  - exposed a public partial `Program` for the integration-test host.
- `Template.Service.Api/appsettings.Development.json`
  - added a non-sensitive local Keycloak placeholder with the `service-api-template` audience and a 60-second clock skew.
- `Template.Service.Api.Tests`
  - added configuration-validation coverage;
  - verified `401` without a token and protection of a controller without `[Authorize]`;
  - verified a valid test token succeeds;
  - verified `401` for a wrong issuer, audience, signature, and expiration;
  - verified the Swagger Bearer definition.

## Security Decisions

- Protection is global; a new controller is not anonymous merely because it lacks `[Authorize]`.
- No business endpoint currently has `[AllowAnonymous]`.
- One configured audience is accepted: `service-api-template`.
- Authority must be an absolute HTTPS URL.
- Clock skew is configurable, defaults to 60 seconds, and is capped at 300 seconds.
- Role mapping is not part of this step. `RoleClientId` is retained for a future policy limited to the agreed Keycloak client namespace.
- Development Swagger is outside the controller fallback policy but remains disabled outside Development.

## Validation Log

- `dotnet build Template.Service.Api.sln --configuration Debug` - passed, 0 errors.
- `dotnet test Template.Service.Api.sln --configuration Debug --no-build` - passed, 40/40.
  - `Framework.Logger.Tests`: 12/12.
  - `Template.Service.Api.Tests`: 28/28.
- Development runtime probe:
  - `/swagger/v1/swagger.json` - HTTP 200;
  - unauthenticated `POST /Product` - HTTP 401.
- Runtime-log search found no Bearer token, Authorization header, or `integration-test-client` claim value.

## Known Notes

- The build still reports the previously documented AutoMapper 12.0.1 `NU1903` and existing nullable warnings; they were not introduced by this JWT scope.
- The external `Configuration=configuration-service/` value requires explicit `--configuration Debug` for local commands.
- Real Keycloak E2E validation awaits environment Authority values and caller-client/audience-mapper confirmation.

## Deployment / Configuration

Each environment must provide:

```text
Authentication__Keycloak__Authority
Authentication__Keycloak__Audience=service-api-template
Authentication__Keycloak__RoleClientId=service-api-template
Authentication__Keycloak__ClockSkewSeconds=60
```

The application intentionally fails to start when the required section is absent or Authority/Audience/clock skew is invalid. These values are not secrets, but they are environment-specific.

## Commits

- Pending commit.

## Follow-up

- Run E2E validation with a real Keycloak access token.
- Confirm the audience mapper for every caller client.
- Explicitly approve every future `[AllowAnonymous]` endpoint.
- Define separate role/policy requirements if business methods no longer share the same access level.

