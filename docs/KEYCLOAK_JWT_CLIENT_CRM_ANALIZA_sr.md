# Keycloak JWT klijent i CRM.Api integracija - analiza

## Status

- Faza: implementirano i lokalno validirano; realni Keycloak/CRM E2E je pending.
- Datum analize: 20. avgust 2026.
- Grana tokom analize: `main`.
- Preduslov: .NET 10 migracija završena i lokalno validirana 20. avgusta 2026.
- Bezbednosni preduslov za javni mock endpoint: serverska JWT zaštita završena u commitu `d4997e2`.
- Referentni dokument: `E:\GIT_2026\rafajlovski-chatbot_2026\keycloakJWT.md`.

## Cilj

Omogućiti da `Service.Api.Template` kao OAuth klijent dobije Keycloak access token pomoću `client_credentials` toka, bezbedno ga kešira i automatski doda samo odlaznim pozivima ka novom named klijentu `CRM.api`.

Dodati referentnu CRM vertikalu, sličnu postojećem `GetProduct` toku, ali sa odvojenim CRM modelima i servisima. Javni `CrmController` endpoint ne sme biti uveden pre globalne serverske JWT zaštite.

## Implementirano stanje

- `HttpClientNames.CRMApi` ima stvarnu named-client vrednost `CRM.api`; `KeycloakToken` je zaseban klijent bez Bearer handlera.
- `KeycloakAccessTokenProvider` koristi `client_credentials`, singleton cache, `TimeProvider`, `SemaphoreSlim` refresh zaključavanje i double-check nakon zaključavanja.
- Refresh buffer je konfigurabilan, a efektivno se ograničava na polovinu kratkog token lifetime-a.
- `BearerTokenHandler` dodaje token samo CRM pozivima i na CRM `401` uslovno invalidira samo korišćeni token, bez automatskog replay-a.
- `ProductApi` i `HRApi` zadržavaju samo correlation handler i ne dobijaju CRM Bearer token.
- Konfiguracija fail-fast proverava HTTPS token URL, client ID, secret, refresh window i HTTPS CRM base address.
- `ClientSecret` nije u `appsettings` fajlovima; Web projekat ima User Secrets identitet, a environment contract je `KeycloakClient__ClientSecret`.
- Implementirana je posebna `CrmController -> CrmBusinessLogic -> CrmService` vertikala sa CRM modelima i privremenom downstream rutom `api/mock/customers`.
- `GET /api/Crm/mock` je zaštićen postojećom globalnom serverskom JWT fallback politikom.

## Očekivano stanje

- Postoje odvojeni named klijenti `KeycloakToken` i `CRM.api`.
- `KeycloakToken` nikada nema `BearerTokenHandler`.
- `CRM.api` ima `BearerTokenHandler` i postojeći `CorrelationIdHandler`.
- Singleton token provider deli jedan cache i refresh lock kroz aplikaciju.
- Token se obnavlja pre isteka, bez paralelnog token-request stampede-a.
- Client secret ne postoji u source control-u niti logovima.
- Product i HR pozivi ne dobijaju CRM Bearer token.
- Postoji zaštićeni mock CRM endpoint kroz poseban controller/business/service tok.

## OAuth i Keycloak trust model

Predloženi Keycloak identiteti:

| Client | Namena |
|---|---|
| `service-api-template` | Confidential client kojim `Service.Api.Template` traži token. |
| `crm-api` | Resource server audience i namespace CRM client rola. |

Za `service-api-template` treba uključiti service account, dodeliti minimalnu CRM client rolu i konfigurisati audience mapper.

Početni predlog role je `crm_read`. Očekivani relevantni token sadržaj:

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

`client_id` identifikuje pozivaoca, a `aud` određuje kom API-ju token sme biti prosleđen. CRM server mora eksplicitno validirati `aud = crm-api`.

## Implementirane komponente

### Konfiguracioni modeli

`KeycloakClientOptions`:

- `TokenUrl`
- `ClientId`
- `ClientSecret`
- `RefreshBeforeExpirySeconds`, podrazumevano 30

CRM base address ostaje u postojećoj `ApiPaths` sekciji kao `CRMApiBaseAddress`.

### Token provider

`IAccessTokenProvider` i `KeycloakAccessTokenProvider` treba da obezbede:

- `CancellationToken` podršku;
- singleton cache;
- `DateTimeOffset` i `TimeProvider` radi testabilnosti;
- `SemaphoreSlim` refresh zaključavanje;
- double-check cache-a nakon zaključavanja;
- bezbedan refresh buffer;
- validaciju `access_token`, `expires_in` i `token_type`;
- uslovni `Invalidate(token)` koji ne briše noviji token zbog zakašnjelog `401` odgovora;
- kontrolisan exception bez slanja CRM zahteva kada token nije moguće dobiti.

### Bearer handler

`BearerTokenHandler`:

1. traži token od providera;
2. postavlja `Authorization: Bearer <token>`;
3. šalje zahtev;
4. na CRM `401` uslovno invalidira korišćeni token;
5. ne ponavlja automatski tekući zahtev u početnoj implementaciji.

Automatski retry se kasnije može dodati samo za potvrđeno idempotentne zahteve, najviše jednom.

### Named klijenti

Implementirana imena:

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

`CRM.api` base address i token URL zahtevaju HTTPS već pri startup-u. Oba klijenta imaju eksplicitan timeout od 30 sekundi.

## Mock CRM vertikala

Implementirane komponente:

- `CrmController`
- `ICrmBusinessLogic` / `CrmBusinessLogic`
- `ICrmService` / `CrmService`
- `CrmRequest` i `CrmResponse` mock modeli
- Autofac registracije za CRM business i service sloj

Implementirani tok:

```text
GET /api/crm/mock
  -> CrmController.GetMockData
  -> CrmBusinessLogic.GetMockData
  -> CrmService.GetMockData
  -> GET <CRMApiBaseAddress>/api/mock/customers
```

Mock modeli ne treba da koriste Product DTO-e. Relativna downstream ruta mora biti jasno označena kao privremena dok se ne dobije stvarni CRM ugovor.

Ako se klijentski deo implementira pre serverskog dela, controller se ne mapira u toj fazi. Preporučeni stvarni redosled je: .NET 10, serverski JWT, zatim klijentski JWT i kompletna CRM vertikala.

## Secret storage

Aplikacioni contract je `KeycloakClient:ClientSecret`, ali vrednost ne sme biti u `appsettings.json`.

| Okruženje | Predlog |
|---|---|
| Lokalni razvoj | .NET User Secrets ili `KeycloakClient__ClientSecret`. |
| CI/CD | Zaštićena pipeline secret promenljiva. |
| Azure produkcija | Key Vault preko Managed Identity ili platformskog Key Vault reference mehanizma. |
| Docker/Kubernetes | Platformski secret mount ili environment promenljiva. |

Secret pripada samo `service-api-template` client-u i ne deli se sa frontend-om ili drugim servisima.

## Pogođeni fajlovi i oblasti

- `Template.Service.Services.Interfaces/Infrastructure/Http/HttpClientNames.cs`
- `Template.Service.DependencyInjection/DependencyInjectionConfig.cs`
- novi auth/options/provider/handler fajlovi u odgovarajućem infrastructure sloju
- `Template.Service.Services.Interfaces` - CRM service contract
- `Template.Service.Services` - CRM service i outbound poziv
- `Template.Service.BusinessLogic.Interfaces` - CRM business contract
- `Template.Service.BusinessLogic` - CRM business implementacija
- `Template.Service.BusinessModel` i/ili `Template.Service.DataModel` - CRM mock modeli prema postojećoj podeli odgovornosti
- `Template.Service.Api/Controllers/CrmController.cs`
- `Template.Service.Api/appsettings*.json` - samo neosetljive placeholder/configuration vrednosti
- `Template.Service.Api.Tests` - token, handler, URI, DI i CRM tok testovi

## Bezbednosna pravila

- Ne logovati token, secret, Authorization header ili kompletan Keycloak odgovor.
- Ne slati zahtev ka CRM-u bez tokena.
- Ne dodavati Bearer handler drugim named klijentima.
- Ne dodavati Bearer handler token klijentu zbog rekurzije.
- Ne retry-ovati `invalid_client`, `401` ili `403` sa token endpointa.
- Ograničeni retry razmatrati samo za mrežne greške, `429` i prolazne `5xx` odgovore.
- Redigovati `Authorization` header u HTTP logovima.
- Validirati HTTPS URL-ove i options konfiguraciju pri startup-u.
- Ako je refresh buffer veći ili jednak trajanju tokena, ograničiti efektivni buffer kako se token ne bi pribavljao pri svakom pozivu.

## Rizici

- Keycloak token može nemati `crm-api` audience bez pravilnog mappera.
- Service account može imati rolu, ali je role scope mapping može izostaviti iz tokena.
- Deljeni ili pogrešan secret povećava blast radius.
- Pogrešan handler pipeline može slati CRM token drugom API-ju ili napraviti rekurziju.
- Token endpoint outage može izazvati seriju sekvencijalnih neuspelih refresh pokušaja; resilience politika mora biti ograničena.
- Javni mock endpoint uveden pre serverske zaštite bio bi anoniman.

## Otvorena pitanja za environment/E2E fazu

- Stvarni `TokenUrl` i realm po okruženju.
- Potvrda naziva `service-api-template`, `crm-api` i `crm_read`.
- Stvarni `CRMApiBaseAddress`.
- Stvarna CRM ruta i HTTP metoda koja će zameniti mock.
- Produkcioni secret provider i procedura rotacije.
- Da li budući CRM write endpoint-i podržavaju idempotency key.

Ove vrednosti ne blokiraju unit-testabilnu implementaciju sa placeholder konfiguracijom, ali blokiraju realni E2E test.

## Pristup validaciji

### Unit testovi token providera

- prazan cache šalje tačno jedan token zahtev;
- validan cache ne šalje HTTP zahtev;
- token u refresh prozoru se obnavlja;
- paralelni pozivi proizvode jedan token zahtev;
- nevalidan token odgovor daje kontrolisan exception;
- `Invalidate(stariToken)` ne briše noviji token;
- cancellation se propagira.

### Handler i named client testovi

- `CRM.api` dobija Bearer i Correlation-ID headere;
- `ProductApi` i `HRApi` ne dobijaju CRM Bearer token;
- `KeycloakToken` nema Bearer handler;
- CRM `401` invalidira samo korišćeni token;
- finalni CRM URI se gradi iz base address-a i relativne rute;
- DI/Autofac razrešava CRM vertikalu.

### Integraciona validacija

- realan token sadrži `aud = crm-api`;
- realan token sadrži potrebnu CRM client rolu;
- CRM prihvata validan token;
- CRM vraća `401` za pogrešan audience i `403` bez role;
- token i secret nisu prisutni u logovima.

## Rezultat validacije i acceptance kriterijumi

- [x] `CRM.api` i `KeycloakToken` su odvojeni named klijenti.
- [x] Token provider je concurrency-safe i testabilan.
- [x] CRM handler ne utiče na Product/HR klijente.
- [x] Operativni secret nije u source control-u niti `appsettings` fajlovima.
- [x] CRM mock vertikala prati postojeću slojevitu arhitekturu.
- [x] Javni CRM endpoint je zaštićen globalnom serverskom politikom.
- [x] Unit i integracioni testovi iz dogovorenog lokalnog scope-a prolaze.

Lokalna validacija 20. avgusta 2026:

- `dotnet build Template.Service.Api.sln --configuration Debug` - passed, 0 grešaka;
- `dotnet test Template.Service.Api.sln --configuration Debug --no-build` - passed, 59/59;
- Development runtime probe - Swagger `200`, `GET /api/Crm/mock` bez inbound tokena `401`;
- konfiguraciona pretraga nije pronašla `ClientSecret`, `client_secret` ni Authorization vrednost u `appsettings` fajlovima.

Realni token/CRM E2E nije izvršen jer stvarni URL-ovi, client secret, audience mapper i CRM ugovor još nisu dostavljeni.

## Preporučeni commit scope

```text
feat: add Keycloak client credentials flow for CRM API
```

Commit se pravi samo na eksplicitan zahtev korisnika.

