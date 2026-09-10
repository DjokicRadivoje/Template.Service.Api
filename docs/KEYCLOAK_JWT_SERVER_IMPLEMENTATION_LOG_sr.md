# Keycloak JWT serverska zaštita Service.Api.Template - Implementation Log

## Context

- Tema: validacija dolaznog Keycloak JWT access tokena
- Grana: `main`
- Datum: 20. avgust 2026.
- Analysis: `docs/KEYCLOAK_JWT_SERVER_ANALIZA_sr.md`
- Status: implementirano i lokalno validirano; pending commit, CI i realni Keycloak E2E.

## Timeline

- Potvrđeno je da `ProductController` nije imao autentifikacionu zaštitu i da je postojao samo `UseAuthorization()` middleware.
- Dodat je .NET 10 JWT Bearer paket i centralna `AddKeycloakAuthentication(...)` registracija.
- Uvedena je fail-fast validacija Authority, Audience i clock skew konfiguracije.
- Uvedena je globalna fallback politika koja štiti sve sadašnje i buduće controller endpoint-e.
- `UseAuthentication()` je postavljen pre `UseAuthorization()`.
- Development Swagger je dopunjen Bearer/JWT security definicijom.
- Dodati su offline konfiguracioni i integracioni testovi sa lokalno potpisanim tokenima.
- Build, 40 testova i kratka runtime provera uspešno su završeni.

## Implementirane izmene

- `Template.Service.Api/Template.Service.Api.csproj`
  - dodat `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.11.
- `Template.Service.Api/Infrastructure/Authentication/KeycloakAuthenticationOptions.cs`
  - uvedeni `Authority`, `Audience`, `RoleClientId` i `ClockSkewSeconds`.
- `Template.Service.Api/Infrastructure/Authentication/KeycloakAuthenticationExtensions.cs`
  - registrovana podrazumevana Bearer šema;
  - uključena provera issuer-a, audience-a, lifetime-a i signing key-a;
  - postavljeni `MapInboundClaims = false`, HTTPS metadata i eksplicitni clock skew;
  - dodata globalna `RequireAuthenticatedUser()` fallback politika;
  - dodati bezbedni log događaji koji ne beleže token, header ni claim vrednosti.
- `Template.Service.Api/Program.cs`
  - pozvana JWT registracija;
  - dodat Swagger Bearer scheme;
  - dodat `UseAuthentication()` pre autorizacije;
  - dodat javni parcijalni `Program` radi integration test hosta.
- `Template.Service.Api/appsettings.Development.json`
  - dodat neosetljivi lokalni Keycloak placeholder sa `service-api-template` audience-om i clock skew-om 60 sekundi.
- `Template.Service.Api.Tests`
  - dodata konfiguraciona validacija;
  - potvrđeni `401` bez tokena i zaštita controller-a bez `[Authorize]`;
  - potvrđen prolaz validnog test tokena;
  - potvrđen `401` za pogrešan issuer, audience, potpis i istekao token;
  - potvrđena Swagger Bearer definicija.

## Bezbednosne odluke

- Zaštita je globalna; novi controller nije anoniman samo zato što nema `[Authorize]`.
- Nijedan poslovni endpoint trenutno nema `[AllowAnonymous]`.
- Prihvata se jedan konfigurisani audience: `service-api-template`.
- Authority mora biti apsolutni HTTPS URL.
- Clock skew je konfigurabilan, podrazumevano 60 sekundi, i ograničen na najviše 300 sekundi.
- Role mapping nije deo ovog koraka. `RoleClientId` je sačuvan za buduću politiku koja će čitati samo dogovoreni Keycloak client namespace.
- Development Swagger ostaje van controller fallback politike, ali se ne uključuje van Development okruženja.

## Validation Log

- `dotnet build Template.Service.Api.sln --configuration Debug` - passed, 0 grešaka.
- `dotnet test Template.Service.Api.sln --configuration Debug --no-build` - passed, 40/40.
  - `Framework.Logger.Tests`: 12/12.
  - `Template.Service.Api.Tests`: 28/28.
- Development runtime probe:
  - `/swagger/v1/swagger.json` - HTTP 200;
  - `POST /Product` bez tokena - HTTP 401.
- Pretraga runtime loga nije pronašla Bearer token, Authorization header ni `integration-test-client` claim vrednost.

## Poznate napomene

- Build i dalje prijavljuje ranije dokumentovan `NU1903` za AutoMapper 12.0.1 i postojeće nullable warning-e; nisu uvedeni ovim JWT scope-om.
- Zbog spoljne `Configuration=configuration-service/` vrednosti lokalne komande zahtevaju eksplicitni `--configuration Debug`.
- Realna Keycloak E2E provera čeka Authority po okruženju i potvrdu caller client/audience mapper konfiguracije.

## Deployment / Configuration

Svako okruženje mora obezbediti:

```text
Authentication__Keycloak__Authority
Authentication__Keycloak__Audience=service-api-template
Authentication__Keycloak__RoleClientId=service-api-template
Authentication__Keycloak__ClockSkewSeconds=60
```

Aplikacija namerno neće startovati ako obavezna sekcija nedostaje ili Authority/Audience/clock skew nisu validni. Vrednosti nisu secret-i, ali su environment-specific.

## Commits

- Pending commit.

## Follow-up

- Izvršiti E2E proveru sa stvarnim Keycloak access tokenom.
- Potvrditi audience mapper za svaki caller client.
- Eksplicitno odobriti svaki budući `[AllowAnonymous]` endpoint.
- Definisati posebne role/policy zahteve ako poslovne metode više ne budu imale isti nivo pristupa.

