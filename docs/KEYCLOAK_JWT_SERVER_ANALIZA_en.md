# Keycloak JWT Server Protection for Service.Api.Template - Analysis

## Status

- Phase: implemented and locally validated; real Keycloak E2E validation is pending.
- Analysis date: August 20, 2026.
- Branch during analysis: `main`.
- Prerequisite: the .NET 10 migration was completed and locally validated on August 20, 2026.
- Order: server-side protection is implemented before the public CRM mock endpoint.
- Reference document: `E:\GIT_2026\rafajlovski-chatbot_2026\keycloakJWT.md`.

## Goal

Protect every existing and future `Service.Api.Template` controller action by validating an incoming Keycloak JWT access token. No controller action is anonymous by default.

The initial authorization requirement is an authenticated caller with a valid token for the `service-api-template` audience. Role-based restrictions are not required for the existing `ProductController`, but the infrastructure must support precise policies later.

## Implemented State

- Added `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.11.
- `AddKeycloakAuthentication(...)` registers the default Bearer scheme and explicit issuer, audience, lifetime, and signing-key validation.
- `MapInboundClaims` is disabled, while clock skew is configurable and constrained to 0-300 seconds.
- Configuration fails fast for a missing section, a non-HTTPS/non-absolute Authority, a missing Audience, or an invalid clock skew.
- A global fallback policy requires an authenticated user for every controller endpoint without relying on individual `[Authorize]` attributes.
- `UseAuthentication()` was added before `UseAuthorization()`.
- Development Swagger now contains a Bearer/JWT security definition.
- Safe authentication events log only the request path and error type, never a token, header, or claim value.
- Role parsing was not introduced because it is outside the initial requirement; `RoleClientId` remains reserved for later policies.

## Expected Behavior

| Scenario | Result |
|---|---|
| No Authorization header | `401 Unauthorized` |
| Token has an invalid signature or is expired | `401 Unauthorized` |
| Token has the wrong issuer | `401 Unauthorized` |
| Token does not contain the `service-api-template` audience | `401 Unauthorized` |
| Token is valid for `service-api-template` | The controller action executes |
| An endpoint later requires a role absent from the token | `403 Forbidden` |
| A new controller has no `[Authorize]` attribute | It remains protected by the fallback policy |
| An endpoint has an approved `[AllowAnonymous]` | It is available without a token |

## Adopted Trust Model

- `Authority`: exact HTTPS Keycloak realm URL per environment.
- `Audience`: `service-api-template`.
- `RoleClientId`: `service-api-template`, only if Keycloak client roles are mapped.
- Accept one agreed issuer and one resource-server audience.
- Do not accept `account`, frontend audiences, or roles from other Keycloak client sections without a documented trust model.
- The incoming token is independent of the `client_credentials` token used by `Service.Api.Template` to call CRM.

Callers must obtain an access token whose `aud` contains `service-api-template`. This requires an appropriate Keycloak Audience/Audience Resolve mapper on the client issuing the caller's token.

## Implemented Solution

### Options and configuration validation

The strongly typed inbound options model `KeycloakAuthenticationOptions` was introduced for:

- `Authority`
- `Audience`
- `RoleClientId`
- `ClockSkewSeconds`, with an initial recommendation of 60

Validate configuration at startup:

- Authority must be an absolute HTTPS URI in real environments;
- Audience is required;
- RoleClientId is required only when role mapping is enabled;
- clock skew cannot be negative.

### JWT Bearer registration

On .NET 10, add a framework-aligned `Microsoft.AspNetCore.Authentication.JwtBearer` reference and register the default Bearer scheme.

Token validation must explicitly verify:

- issuer;
- audience;
- lifetime;
- signing key;
- agreed clock skew.

Set `MapInboundClaims = false` so original JWT claim names remain predictable.

### Global authorization fallback policy

Use a fallback policy:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

This protects the existing `ProductController`, the future `CrmController`, and every new controller action without relying on manually applied `[Authorize]` attributes.

`[AllowAnonymous]` is allowed only for a previously agreed endpoint. No anonymous business endpoint is currently confirmed.

### Middleware order

```text
UseCorrelationId
  -> UseHttpsRedirection
  -> UseAuthentication
  -> UseAuthorization
  -> MapControllers
```

`UseAuthentication()` must precede `UseAuthorization()`.

### Role mapping

The initial scope requires only a valid token, so a custom role parser is not necessary for basic protection. If introduced to support future role policies, it may read only:

```text
resource_access.<RoleClientId>.roles
```

The parser must validate the JSON structure, avoid duplicates, and fail authentication in a controlled way when the agreed claim is invalid. It must not aggregate roles from every client section.

### Swagger

Swagger remains available only in Development. Add a Bearer security scheme so a developer can enter an access token and call protected methods.

The fallback policy does not automatically protect arbitrary middleware such as Swagger UI. If Swagger is ever enabled outside Development, it requires separate protection or a network restriction.

## Configuration

Non-sensitive example:

```json
{
  "Authentication": {
    "Keycloak": {
      "Authority": "https://<keycloak-host>/realms/<realm>",
      "Audience": "service-api-template",
      "RoleClientId": "service-api-template",
      "ClockSkewSeconds": 60
    }
  }
}
```

Authority and audience are not secrets but must be environment-specific. The documentation does not contain real internal host values.

## Affected Files and Areas

- `Template.Service.Api/Template.Service.Api.csproj` - JWT Bearer package on .NET 10.
- `Template.Service.Api/Program.cs` - authentication registration, fallback policy, middleware order, and Swagger security scheme.
- new options/extension files in the `Template.Service.Api` infrastructure area.
- `Template.Service.Api/appsettings*.json` - non-sensitive placeholder/configuration keys.
- `Template.Service.Api.Tests` - authorization and token-validation tests.
- all existing and future controllers - behavior changes globally even without per-controller attributes.

## Security Rules

- Validate signature, issuer, audience, and lifetime for every token.
- Do not log tokens or Authorization headers.
- Log only safe data such as status, path, and correlation ID.
- Do not return token-validation exception details to the caller.
- Use `401` for an absent/invalid token and `403` for a valid token without permission.
- Do not expand the accepted audience list to make a temporary test pass.
- Do not disable HTTPS metadata in real environments.
- Test clock skew explicitly; the library default must not remain an unnoticed decision.

## Risks

- An incorrect Keycloak audience mapper can reject every legitimate caller.
- Global protection can break monitoring or integrations that relied on anonymous access.
- Swagger/health expectations can be unclear.
- Broad role mapping can accept a same-named role from another client.
- Startup validation can intentionally stop the application when an environment lacks required configuration.

## Open Questions for the Environment/E2E Phase

- Actual `Authority` per environment.
- Which Keycloak clients issue tokens for real callers and how they obtain the `service-api-template` audience.
- Whether a health endpoint requiring explicit `[AllowAnonymous]` will be introduced.
- Which role/policy checks are introduced after basic authentication.

The implementation adopts the `service-api-template` inbound audience, Development-only Swagger, and a 60-second clock skew. Environment configuration can change these values without code changes, except that changing the accepted audience remains an explicit trust-model decision.

Real Keycloak values do not block implementation with test configuration, but they block real end-to-end validation.

## Validation Approach

### Static and DI validation

- the authentication scheme and options resolve;
- the application fails fast on missing or invalid configuration;
- middleware order is correct;
- endpoint metadata contains the fallback authorization requirement.

### API integration tests

- calling `ProductController` without a token returns `401`;
- a valid locally generated test token for `service-api-template` succeeds;
- a wrong issuer, audience, signature, or expiration returns `401`;
- a new test controller without `[Authorize]` is protected by the fallback policy;
- an explicit `[AllowAnonymous]` test endpoint, if approved, remains available;
- a role policy returns `403` for a valid token without the role.

Tests must not depend on a real Keycloak server being available. The real realm is used in a separate environment integration check.

### Manual/E2E validation

- decode a test token without logging its value and verify issuer/audience;
- call the API without a token, with an invalid token, and with a valid token;
- confirm that logs do not contain the token;
- confirm Swagger Bearer input in Development mode.

## Validation Result and Acceptance Criteria

- [x] All controller endpoints require authentication by default.
- [x] `ProductController` without a token returns `401`.
- [x] A valid locally signed `service-api-template` test token succeeds.
- [x] A wrong issuer/audience/signature/lifetime returns `401`.
- [x] `UseAuthentication()` precedes `UseAuthorization()`.
- [x] Swagger supports Bearer input in Development mode.
- [x] There are no unintentionally anonymous existing business endpoints; the fallback policy also covers future controllers.
- [x] Tests require no network access to a real Keycloak server.

Local validation on August 20, 2026:

- `dotnet build Template.Service.Api.sln --configuration Debug` - passed, 0 errors;
- `dotnet test Template.Service.Api.sln --configuration Debug --no-build` - passed, 40/40;
- Development runtime probe - Swagger `200`, unauthenticated `POST /Product` `401`;
- runtime-log search found no token, Authorization header, or test claim value.

A real Keycloak token was not tested because the environment Authority and caller-client configuration have not yet been provided.

## Recommended Commit Scope

```text
feat: require Keycloak JWT for Service API Template endpoints
```

A commit is created only upon the user's explicit request.

