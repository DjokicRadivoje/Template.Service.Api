# Service.Api.Template Keycloak JWT Server Protection

## Summary

Every current and future `Service.Api.Template` controller endpoint now requires a valid Keycloak JWT access token for the `service-api-template` audience by default.

## Implemented Changes

- Added the .NET 10 JWT Bearer handler.
- Enabled issuer, audience, lifetime, and signing-key validation.
- Introduced fail-fast Keycloak configuration validation.
- Added a global fallback authorization policy requiring an authenticated user.
- `UseAuthentication()` now executes before `UseAuthorization()`.
- Development Swagger supports Bearer token input.
- Authentication logs contain no token, Authorization header, or claim values.

## Request Flow

```text
HTTP request
  -> JWT Bearer authentication
  -> global RequireAuthenticatedUser policy
  -> controller/action
```

A missing or invalid token returns `401`. A future role/policy denial for a valid authenticated caller will return `403`.

## Configuration

Each environment must configure the `Authentication:Keycloak` values: HTTPS `Authority`, `Audience`, `RoleClientId`, and `ClockSkewSeconds`. The current audience is `service-api-template`, with an initial 60-second clock skew.

## Validation

- Build: passed, 0 errors.
- Tests: 40/40 passed.
- Valid local test token: HTTP 200 on the probe controller.
- Missing token, wrong issuer/audience/signature, or expired token: HTTP 401.
- Runtime: Swagger HTTP 200; unauthenticated `POST /Product` HTTP 401.

## Open Items

- Actual Authority per environment and real Keycloak E2E validation.
- Caller-client audience mapper configuration.
- Future role/policy decisions and an explicitly approved anonymous health endpoint, if required.

