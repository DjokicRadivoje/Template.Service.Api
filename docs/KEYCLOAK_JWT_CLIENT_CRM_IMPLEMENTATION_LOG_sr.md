# Keycloak JWT klijent i CRM.api integracija - Implementation Log

## Context

- Tema: odlazni Keycloak `client_credentials` token i CRM mock vertikala
- Grana: `main`
- Datum: 20. avgust 2026.
- Analysis: `docs/KEYCLOAK_JWT_CLIENT_CRM_ANALIZA_sr.md`
- Preduslov: serverska JWT zaštita, commit `d4997e2`
- Status: implementirano i lokalno validirano; pending commit, CI i realni Keycloak/CRM E2E.

## Timeline

- Potvrđena je čista grana osim ranije postojeće `../.github/` putanje.
- Dodat je named client sa tačnom vrednošću `CRM.api` i odvojeni `KeycloakToken` klijent.
- Implementirani su strongly typed client options, singleton token provider i CRM-only Bearer handler.
- Dodat je kompletan `CrmController -> CrmBusinessLogic -> CrmService` mock tok.
- Dodati su provider, handler, named-client, DI, service, controller i inbound authorization testovi.
- Prvi build bio je blokiran ranije pokrenutim lokalnim API procesom koji je zaključao DLL fajlove; proces je identifikovan i zaustavljen.
- Prvi integration test run potvrdio je fail-fast ponašanje, ali je test secret bio dodat prekasno u test-host lifecycle-u; factory je korigovan sa ranim host setting-om.
- Posle korekcije build, 59 testova i runtime probe prošli su uspešno.

## Implementacione napomene

- `HttpClientNames.CRMApi` - vrednost je `CRM.api`; `KeycloakToken` ostaje zaseban named client.
- `KeycloakAccessTokenProvider`
  - šalje `grant_type=client_credentials` form request;
  - kešira token u singleton instanci;
  - koristi `TimeProvider` i `SemaphoreSlim` sa double-check obrascem;
  - osvežava token pre isteka i ograničava buffer na polovinu kratkog lifetime-a;
  - validira `access_token`, pozitivan `expires_in` i `Bearer` token type;
  - uslovna `Invalidate(token)` operacija ne briše noviji token.
- `BearerTokenHandler`
  - dodaje Authorization header samo CRM pipeline-u;
  - na CRM `401` invalidira korišćeni token;
  - ne replay-uje tekući zahtev.
- `DependencyInjectionConfig`
  - `KeycloakToken` nema Bearer handler;
  - `CRM.api` ima Bearer pa correlation handler;
  - Product/HR pipeline nije dobio CRM token;
  - oba nova klijenta imaju timeout 30 sekundi i redakciju Authorization header-a;
  - token URL i CRM base address moraju biti apsolutni HTTPS URI-ji.
- CRM mock endpoint je `GET /api/Crm/mock`, a privremena downstream ruta `api/mock/customers`.
- Globalna serverska fallback politika automatski štiti novi controller.

## Secret i konfiguracija

- `ClientSecret` nije upisan u `appsettings` fajlove.
- Web projekat ima `UserSecretsId` za lokalni razvoj.
- Svako okruženje mora obezbediti:

```text
ApiPaths__CRMApiBaseAddress
KeycloakClient__TokenUrl
KeycloakClient__ClientId
KeycloakClient__ClientSecret
KeycloakClient__RefreshBeforeExpirySeconds
```

- Aplikacija namerno neće startovati bez client secret-a ili sa nevalidnim URL/options vrednostima.
- Stvarni secret, token i kompletan token response ne loguju se.

## Validation Log

- `dotnet build Template.Service.Api.sln --configuration Debug` - passed, 0 grešaka.
- `dotnet test Template.Service.Api.sln --configuration Debug --no-build` - passed, 59/59.
  - `Framework.Logger.Tests`: 12/12.
  - `Template.Service.Api.Tests`: 47/47.
- Pokriveni su cache, refresh, concurrency, invalid response, stale-token invalidation i cancellation scenariji.
- Potvrđeno je da samo `CRM.api` dobija Bearer token, dok Product/HR i `KeycloakToken` ne dobijaju CRM token.
- Development runtime probe:
  - Swagger - HTTP 200;
  - `GET /api/Crm/mock` bez inbound JWT-a - HTTP 401.
- `appsettings` pretraga nije pronašla operativni client secret ni Authorization vrednost.

## Poznate napomene

- Build i dalje prijavljuje ranije dokumentovan AutoMapper 12.0.1 `NU1903`; nije uveden ovim scope-om.
- Stvarna CRM ruta, payload i HTTP metoda nisu potvrđeni; trenutna ruta je namerno mock primer.
- Nije uveden automatski retry CRM zahteva da se ne bi nenamerno ponovio budući ne-idempotentan poziv.
- Realni Keycloak/CRM E2E čeka environment URL-ove, secret, service-account uloge i audience mapper.

## Commits

- Pending commit.

## Follow-up

- Potvrditi `service-api-template`, `crm-api` audience i `crm_read` rolu.
- Konfigurisati secret provider i rotaciju po okruženju.
- Zameniti mock rutu stvarnim CRM ugovorom.
- Ako se dodaju write operacije, pre retry-a potvrditi idempotency strategiju.

