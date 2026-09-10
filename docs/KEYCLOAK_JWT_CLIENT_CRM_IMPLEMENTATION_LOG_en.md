# Keycloak JWT Client and CRM.api Integration - Implementation Log

## Context

- Topic: outbound Keycloak `client_credentials` token and CRM mock vertical
- Branch: `main`
- Date: August 20, 2026
- Analysis: `docs/KEYCLOAK_JWT_CLIENT_CRM_ANALIZA_en.md`
- Prerequisite: server-side JWT protection, commit `d4997e2`
- Status: implemented and locally validated; commit, CI, and real Keycloak/CRM E2E are pending.

## Timeline

- Confirmed a clean branch except for the pre-existing `../.github/` path.
- Added the named client with the exact value `CRM.api` and a separate `KeycloakToken` client.
- Implemented strongly typed client options, a singleton token provider, and a CRM-only Bearer handler.
- Added the complete `CrmController -> CrmBusinessLogic -> CrmService` mock flow.
- Added provider, handler, named-client, DI, service, controller, and inbound-authorization tests.
- The first build was blocked by an earlier local API process locking DLL files; the process was identified and stopped.
- The first integration run confirmed fail-fast behavior, but the test secret was inserted too late in the test-host lifecycle; the factory was corrected with an early host setting.
- The build, 59 tests, and runtime probe then passed.

## Implementation Notes

- `HttpClientNames.CRMApi` - value is `CRM.api`; `KeycloakToken` remains a separate named client.
- `KeycloakAccessTokenProvider`
  - sends a `grant_type=client_credentials` form request;
  - caches the token in a singleton instance;
  - uses `TimeProvider` and a `SemaphoreSlim` double-check pattern;
  - refreshes before expiration and caps the buffer at half of a short lifetime;
  - validates `access_token`, positive `expires_in`, and the `Bearer` token type;
  - conditional `Invalidate(token)` does not remove a newer token.
- `BearerTokenHandler`
  - adds the Authorization header only to the CRM pipeline;
  - invalidates the used token on CRM `401`;
  - does not replay the current request.
- `DependencyInjectionConfig`
  - `KeycloakToken` has no Bearer handler;
  - `CRM.api` has the Bearer and correlation handlers;
  - Product/HR pipelines do not receive the CRM token;
  - both new clients have a 30-second timeout and Authorization-header redaction;
  - token URL and CRM base address must be absolute HTTPS URIs.
- The CRM mock endpoint is `GET /api/Crm/mock`, with temporary downstream path `api/mock/customers`.
- The global server fallback policy automatically protects the new controller.

## Secret and Configuration

- `ClientSecret` is absent from appsettings files.
- The Web project has a `UserSecretsId` for local development.
- Each environment must provide:

```text
ApiPaths__CRMApiBaseAddress
KeycloakClient__TokenUrl
KeycloakClient__ClientId
KeycloakClient__ClientSecret
KeycloakClient__RefreshBeforeExpirySeconds
```

- The application intentionally refuses to start without a client secret or with invalid URL/options values.
- The real secret, token, and complete token response are never logged.

## Validation Log

- `dotnet build Template.Service.Api.sln --configuration Debug` - passed, 0 errors.
- `dotnet test Template.Service.Api.sln --configuration Debug --no-build` - passed, 59/59.
  - `Framework.Logger.Tests`: 12/12.
  - `Template.Service.Api.Tests`: 47/47.
- Cache, refresh, concurrency, invalid response, stale-token invalidation, and cancellation scenarios are covered.
- Only `CRM.api` receives the Bearer token; Product/HR and `KeycloakToken` do not receive the CRM token.
- Development runtime probe:
  - Swagger - HTTP 200;
  - unauthenticated `GET /api/Crm/mock` - HTTP 401.
- Appsettings search found no operational client secret or Authorization value.

## Known Notes

- The build still reports the previously documented AutoMapper 12.0.1 `NU1903`; it was not introduced by this scope.
- The real CRM route, payload, and HTTP method are not confirmed; the current route is intentionally a mock example.
- Automatic CRM request retry was not introduced to avoid accidentally replaying a future non-idempotent call.
- Real Keycloak/CRM E2E awaits environment URLs, the secret, service-account roles, and the audience mapper.

## Commits

- Pending commit.

## Follow-up

- Confirm `service-api-template`, the `crm-api` audience, and the `crm_read` role.
- Configure a secret provider and rotation per environment.
- Replace the mock route with the real CRM contract.
- Confirm an idempotency strategy before retrying any future write operation.

