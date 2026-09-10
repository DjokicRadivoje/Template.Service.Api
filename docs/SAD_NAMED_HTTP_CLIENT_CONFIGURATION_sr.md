# Named HttpClient konfiguracija

## Summary

`ProductService` više ne koristi hardkodovan puni URL. Product API base address dolazi iz konfiguracije, a HTTP poziv koristi named klijent `ProductApi`.

## Implemented Changes

- Dodat `ApiPaths:ProductApiBaseAddress` u `appsettings.Development.json`.
- Dodat centralni naziv `HttpClientNames.ProductApi`.
- Named klijent je registrovan sa postojećim correlation handlerom.
- `ProductService` koristi `IHttpClientFactory` i relativnu putanju `users`.
- Dodati testovi za izbor named klijenta, formiranje request URI-ja i Autofac resolution.

## Affected Components

- `Template.Service.Api` - konfiguracija i registracija klijenta.
- `Template.Service.Services.Interfaces` - centralni naziv klijenta.
- `Template.Service.Services` - korišćenje named klijenta.
- `Template.Service.Api.Tests` - ciljani test.

## Configuration / Deployment Impact

Svako okruženje mora obezbediti `ApiPaths:ProductApiBaseAddress`. Development vrednost je uključena u `appsettings.Development.json`.

## Validation

- Solution build: uspešan, 0 grešaka i 2 ranije postojeća nullable upozorenja.
- Testovi: 23/23 uspešno.

## Further Improvements

- Za nove downstream API-je dodavati zasebne named klijente, adrese i politike otpornosti.

## Change Log - 2026-08-19

- Dodat je named klijent `HRApi` uz postojeći `ProductApi`.
- Registracija pojedinačnih klijenata premeštena je iz `Program.cs` u `Template.Service.DependencyInjection` composition root.
- `Program.cs` koristi jedan delegirajući poziv za registraciju svih servisnih HTTP klijenata.
- `ProductService` kreira oba potrebna klijenta u konstruktoru kada DI razreši servis; klijenti se ne kreiraju u pojedinačnim metodama.
- Oba klijenta koriste correlation handler.
- Build je uspešan, a testovi prolaze (26/26).

