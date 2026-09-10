# Keycloak JWT Client and CRM.api Integration

## Summary

`Service.Api.Template` now obtains and caches a Keycloak access token through the `client_credentials` flow and attaches it exclusively to outbound calls made by the `CRM.api` named client. A protected CRM mock flow was added following the existing layered architecture.

## Implemented Changes

- Separate `KeycloakToken` and `CRM.api` named clients.
- Concurrency-safe singleton token cache with early refresh.
- CRM-only Bearer handler with conditional token invalidation on `401`.
- Product/HR and token clients never receive the CRM Authorization header.
- New `GET /api/Crm/mock` controller/business/service flow targeting the temporary downstream path `api/mock/customers`.
- Fail-fast HTTPS/options/secret validation and Authorization-header redaction.

## Configuration / Deployment Impact

`CRMApiBaseAddress`, the Keycloak token URL, client ID, client secret, and refresh window are required. The client secret must come from User Secrets, an environment variable, or a production secret provider and is absent from appsettings files.

## Validation

- Build: passed, 0 errors.
- Tests: 59/59 passed.
- Cache, refresh, concurrency, invalidation, cancellation, and named-client isolation scenarios are verified.
- Runtime: Swagger HTTP 200; CRM mock without an inbound JWT returns HTTP 401.

## Further Improvements

- Run E2E validation with a real Keycloak token and CRM service.
- Confirm audience/role mapping and replace the mock route with the real CRM contract.
- Add retries only for confirmed idempotent calls.

