# Keycloak JWT klijent i CRM.api integracija

## Summary

`Service.Api.Template` sada pribavlja i kešira Keycloak access token kroz `client_credentials` tok i dodaje ga isključivo odlaznim pozivima named klijenta `CRM.api`. Dodat je zaštićeni CRM mock tok po postojećoj slojevitoj arhitekturi.

## Implemented Changes

- Odvojeni `KeycloakToken` i `CRM.api` named klijenti.
- Concurrency-safe singleton token cache sa ranim refresh-om.
- CRM-only Bearer handler sa uslovnom invalidacijom tokena na `401`.
- Product/HR i token klijent ne dobijaju CRM Authorization header.
- Novi `GET /api/Crm/mock` controller/business/service tok prema privremenoj downstream ruti `api/mock/customers`.
- Fail-fast HTTPS/options/secret validacija i Authorization header redakcija.

## Configuration / Deployment Impact

Potrebni su `CRMApiBaseAddress`, Keycloak token URL, client ID, client secret i refresh window. Client secret mora doći iz User Secrets, environment promenljive ili produkcionog secret providera i nije deo `appsettings` fajlova.

## Validation

- Build: passed, 0 grešaka.
- Testovi: 59/59 passed.
- Potvrđeni cache, refresh, concurrency, invalidacija, cancellation i named-client isolation scenariji.
- Runtime: Swagger HTTP 200; CRM mock bez inbound JWT-a HTTP 401.

## Further Improvements

- Izvršiti E2E sa stvarnim Keycloak tokenom i CRM servisom.
- Potvrditi audience/role mapiranje i zameniti mock rutu stvarnim CRM ugovorom.
- Retry uvoditi samo za potvrđeno idempotentne pozive.

