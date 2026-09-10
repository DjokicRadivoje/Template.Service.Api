# Named HttpClient configuration - Implementation Log

## Context

- Ticket: Nije naveden
- Branch: `main`
- Date: 2026-08-18
- Main document: `docs/NAMED_HTTP_CLIENT_CONFIGURATION_ANALIZA_I_DOGOVOR.md`

## Implemented Changes

- Dodat je `ApiPaths:ProductApiBaseAddress` u development konfiguraciju.
- Dodat je centralni naziv `HttpClientNames.ProductApi`.
- `Program.cs` validira base address i registruje named klijent sa correlation handlerom.
- `ProductService` koristi `IHttpClientFactory`, named klijent i relativnu putanju `users`.
- `Template.Service.Services` direktno referencira `Microsoft.Extensions.Http`.
- Dodati su ciljani `ProductService` i Autofac resolution testovi.

## Validation Log

- `dotnet build Template.Service.Api.sln -c Debug` - passed, 0 errors; 2 ranije postojeća nullable upozorenja.
- `dotnet test Template.Service.Api.sln -c Debug --no-build --no-restore` - passed, 23/23.
- `git diff --check` - passed.

## Commits

- Pending commit.

## Deployment / Environment

- Development base address: `ApiPaths:ProductApiBaseAddress` u `appsettings.Development.json`.
- Ostala okruženja moraju obezbediti isti konfiguracioni ključ.
- Nema database ili migracionih promena.

## Follow-up

- Dodavati zasebne named klijente i konfiguracione ključeve tek kada se uvedu novi downstream API-ji.

## Multiple Client Composition Root Update - 2026-08-19

### Implemented Changes

- Registracija `ProductApi` klijenta uklonjena je iz `Program.cs`.
- `Program.cs` sada jednim pozivom delegira registraciju klijenata u `DependencyInjectionConfig.ConfigureHttpClients`.
- `Template.Service.DependencyInjection` registruje `ProductApi` i `HRApi`, validira oba base address ključa i dodaje correlation handler na oba pipeline-a.
- `ProductService` zadržava konstruktor-based kreiranje oba potrebna klijenta; nije uvedeno kreiranje unutar pojedinačnih metoda.
- Testovi potvrđuju obe base adrese, Autofac resolution, oba relativna endpointa i validaciju neispravne konfiguracije.

### Validation

- `dotnet build Template.Service.Api.sln -c Debug` - passed, 0 errors; 4 postojeća nullable upozorenja u DTO modelima.
- `dotnet test Template.Service.Api.Tests/Template.Service.Api.Tests.csproj -c Debug --no-build --no-restore` - passed, 14/14.
- `dotnet test Template.Service.Api.sln -c Debug --no-build --no-restore` - passed, 26/26.
- `git diff --check` - passed.

### Deployment / Environment

- Svako okruženje mora obezbediti i `ApiPaths:ProductApiBaseAddress` i `ApiPaths:HRApiBaseAddress`.
- Nema database ili migracionih promena.

