# Keycloak JWT Client and CRM.Api Integration - Analysis

## Status

- Phase: implemented and locally validated; real Keycloak/CRM E2E is pending.
- Analysis date: August 20, 2026.
- Branch during analysis: `main`.
- Prerequisite: the .NET 10 migration was completed and locally validated on August 20, 2026.
- Security prerequisite for the public mock endpoint: server-side JWT protection completed in commit `d4997e2`.
- Reference document: `E:\GIT_2026\rafajlovski-chatbot_2026\keycloakJWT.md`.

## Goal

Enable `Service.Api.Template`, acting as an OAuth client, to obtain a Keycloak access token through the `client_credentials` flow, cache it safely, and automatically attach it only to outbound calls made through the new `CRM.api` named client.

Add a reference CRM vertical similar to the existing `GetProduct` flow, but with separate CRM models and services. The public `CrmController` endpoint must not be introduced before global server-side JWT protection is active.

## Implemented State

- `HttpClientNames.CRMApi` has the actual named-client value `CRM.api`; `KeycloakToken` is separate and has no Bearer handler.
- `KeycloakAccessTokenProvider` uses `client_credentials`, a singleton cache, `TimeProvider`, a `SemaphoreSlim` refresh lock, and a second cache check after lock acquisition.
- The refresh buffer is configurable and effectively capped at half of a short token lifetime.
- `BearerTokenHandler` adds a token only to CRM calls and conditionally invalidates only the used token on a CRM `401`, without replaying the request.
- `ProductApi` and `HRApi` retain only the correlation handler and never receive the CRM Bearer token.
- Startup validation checks the HTTPS token URL, client ID, secret, refresh window, and HTTPS CRM base address.
- `ClientSecret` is absent from appsettings; the Web project has a User Secrets ID and the environment contract is `KeycloakClient__ClientSecret`.
- A dedicated `CrmController -> CrmBusinessLogic -> CrmService` vertical exists with CRM models and the temporary downstream route `api/mock/customers`.
- `GET /api/Crm/mock` is protected by the existing global server-side JWT fallback policy.

## Expected State

- Separate `KeycloakToken` and `CRM.api` named clients exist.
- `KeycloakToken` never has a `BearerTokenHandler`.
- `CRM.api` has both `BearerTokenHandler` and the existing `CorrelationIdHandler`.
- A singleton token provider shares one cache and refresh lock across the application.
- The token is renewed before expiration without a parallel token-request stampede.
- The client secret never appears in source control or logs.
- Product and HR calls do not receive the CRM Bearer token.
- A protected mock CRM endpoint exists through a dedicated controller/business/service flow.

## OAuth and Keycloak Trust Model

Proposed Keycloak identities:

| Client | Purpose |
|---|---|
| `service-api-template` | Confidential client used by `Service.Api.Template` to request a token. |
| `crm-api` | Resource-server audience and CRM client-role namespace. |

Enable a service account for `service-api-template`, assign the minimum CRM client role, and configure an audience mapper.

The initial role proposal is `crm_read`. Expected relevant token content:

```json
{
  "aud": ["crm-api"],
  "resource_access": {
    "crm-api": {
      "roles": ["crm_read"]
    }
  }
}
```

`client_id` identifies the caller, while `aud` determines which API may receive the token. The CRM server must explicitly validate `aud = crm-api`.

## Implemented Components

### Configuration models

`KeycloakClientOptions`:

- `TokenUrl`
- `ClientId`
- `ClientSecret`
- `RefreshBeforeExpirySeconds`, defaulting to 30

The CRM base address remains in the existing `ApiPaths` section as `CRMApiBaseAddress`.

### Token provider

`IAccessTokenProvider` and `KeycloakAccessTokenProvider` must provide:

- `CancellationToken` support;
- a singleton cache;
- `DateTimeOffset` and `TimeProvider` for testability;
- `SemaphoreSlim` refresh locking;
- a second cache check after acquiring the lock;
- a safe refresh buffer;
- validation of `access_token`, `expires_in`, and `token_type`;
- conditional `Invalidate(token)` that does not remove a newer token because of a delayed `401` response;
- a controlled exception without sending a CRM request when a token cannot be obtained.

### Bearer handler

`BearerTokenHandler`:

1. obtains a token from the provider;
2. sets `Authorization: Bearer <token>`;
3. sends the request;
4. conditionally invalidates the used token when CRM returns `401`;
5. does not automatically replay the current request in the initial implementation.

Automatic retry can be added later only for confirmed idempotent requests and at most once.

### Named clients

Implemented names:

```csharp
public const string CRMApi = "CRM.api";
public const string KeycloakToken = "KeycloakToken";
```

Pipeline:

```text
KeycloakAccessTokenProvider
  -> KeycloakToken HttpClient
  -> Keycloak token endpoint

CrmService
  -> CRM.api HttpClient
  -> BearerTokenHandler
  -> CorrelationIdHandler
  -> CRM.Api
```

The `CRM.api` base address and token URL are required to use HTTPS at startup. Both clients have an explicit 30-second timeout.

## Mock CRM Vertical

Implemented components:

- `CrmController`
- `ICrmBusinessLogic` / `CrmBusinessLogic`
- `ICrmService` / `CrmService`
- `CrmRequest` and `CrmResponse` mock models
- Autofac registrations for the CRM business and service layers

Implemented flow:

```text
GET /api/crm/mock
  -> CrmController.GetMockData
  -> CrmBusinessLogic.GetMockData
  -> CrmService.GetMockData
  -> GET <CRMApiBaseAddress>/api/mock/customers
```

Mock models must not reuse Product DTOs. The relative downstream route must be clearly marked as temporary until the real CRM contract is available.

If the client-side part is implemented before the server-side part, the controller must not be mapped in that phase. The recommended actual order is .NET 10, server-side JWT, and then client-side JWT with the complete CRM vertical.

## Secret Storage

The application contract is `KeycloakClient:ClientSecret`, but the value must not be stored in `appsettings.json`.

| Environment | Proposal |
|---|---|
| Local development | .NET User Secrets or `KeycloakClient__ClientSecret`. |
| CI/CD | Protected pipeline secret variable. |
| Azure production | Key Vault through Managed Identity or a platform Key Vault reference mechanism. |
| Docker/Kubernetes | Platform secret mount or environment variable. |

The secret belongs only to the `service-api-template` client and is not shared with the frontend or other services.

## Affected Files and Areas

- `Template.Service.Services.Interfaces/Infrastructure/Http/HttpClientNames.cs`
- `Template.Service.DependencyInjection/DependencyInjectionConfig.cs`
- new auth/options/provider/handler files in the appropriate infrastructure layer
- `Template.Service.Services.Interfaces` - CRM service contract
- `Template.Service.Services` - CRM service and outbound call
- `Template.Service.BusinessLogic.Interfaces` - CRM business contract
- `Template.Service.BusinessLogic` - CRM business implementation
- `Template.Service.BusinessModel` and/or `Template.Service.DataModel` - CRM mock models according to the existing responsibility split
- `Template.Service.Api/Controllers/CrmController.cs`
- `Template.Service.Api/appsettings*.json` - non-sensitive placeholder/configuration values only
- `Template.Service.Api.Tests` - token, handler, URI, DI, and CRM-flow tests

## Security Rules

- Do not log tokens, secrets, Authorization headers, or complete Keycloak responses.
- Do not send a CRM request without a token.
- Do not add the Bearer handler to other named clients.
- Do not add the Bearer handler to the token client because that would create recursion.
- Do not retry `invalid_client`, `401`, or `403` responses from the token endpoint.
- Consider bounded retry only for network errors, `429`, and transient `5xx` responses.
- Redact the `Authorization` header in HTTP logs.
- Validate HTTPS URLs and options configuration at startup.
- If the refresh buffer is greater than or equal to token lifetime, limit the effective buffer so that every call does not obtain a new token.

## Risks

- A Keycloak token can omit the `crm-api` audience without the correct mapper.
- A service account can own a role that is still omitted from the token by role-scope mappings.
- A shared or incorrect secret increases the blast radius.
- An incorrect handler pipeline can send a CRM token to another API or cause recursion.
- A token-endpoint outage can cause a sequence of failed refresh attempts; resilience must remain bounded.
- A public mock endpoint introduced before server-side protection would be anonymous.

## Open Questions for the Environment/E2E Phase

- Actual `TokenUrl` and realm per environment.
- Confirmation of the `service-api-template`, `crm-api`, and `crm_read` names.
- Actual `CRMApiBaseAddress`.
- Actual CRM route and HTTP method replacing the mock.
- Production secret provider and rotation procedure.
- Whether future CRM write endpoints support an idempotency key.

These values do not block a unit-testable implementation with placeholder configuration, but they block a real end-to-end test.

## Validation Approach

### Token-provider unit tests

- an empty cache sends exactly one token request;
- a valid cached token performs no HTTP request;
- a token in the refresh window is renewed;
- concurrent calls produce one token request;
- an invalid token response throws a controlled exception;
- `Invalidate(oldToken)` does not remove a newer token;
- cancellation is propagated.

### Handler and named-client tests

- `CRM.api` receives Bearer and Correlation-ID headers;
- `ProductApi` and `HRApi` do not receive the CRM Bearer token;
- `KeycloakToken` has no Bearer handler;
- a CRM `401` invalidates only the used token;
- the final CRM URI is composed from the base address and relative route;
- DI/Autofac resolves the CRM vertical.

### Integration validation

- a real token contains `aud = crm-api`;
- a real token contains the required CRM client role;
- CRM accepts a valid token;
- CRM returns `401` for the wrong audience and `403` without the role;
- tokens and secrets are absent from logs.

## Validation Result and Acceptance Criteria

- [x] `CRM.api` and `KeycloakToken` are separate named clients.
- [x] The token provider is concurrency-safe and testable.
- [x] The CRM handler does not affect Product/HR clients.
- [x] The operational secret is absent from source control and appsettings.
- [x] The CRM mock vertical follows the existing layered architecture.
- [x] The public CRM endpoint is protected by the global server policy.
- [x] Unit and integration tests in the agreed local scope pass.

Local validation on August 20, 2026:

- `dotnet build Template.Service.Api.sln --configuration Debug` - passed, 0 errors;
- `dotnet test Template.Service.Api.sln --configuration Debug --no-build` - passed, 59/59;
- Development runtime probe - Swagger `200`, unauthenticated `GET /api/Crm/mock` `401`;
- configuration search found no `ClientSecret`, `client_secret`, or Authorization value in appsettings files.

Real token/CRM E2E was not run because actual URLs, the client secret, audience mapper, and CRM contract have not yet been provided.

## Recommended Commit Scope

```text
feat: add Keycloak client credentials flow for CRM API
```

A commit is created only upon the user's explicit request.

