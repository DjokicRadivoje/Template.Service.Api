# Named HttpClient konfiguracija - analiza i dogovor

## Kontekst

`ProductService` je koristio podrazumevani `HttpClient` registrovan sa praznim imenom i hardkodovan pun URL `https://jsonplaceholder.typicode.com/users`. Takav pristup vezuje servis za konkretno okruženje i otežava dodavanje novih downstream API-ja.

## Dogovoreno rešenje

- U `appsettings.Development.json` uvodi se sekcija `ApiPaths`, u strukturi postojećeg aplikacionog primera.
- Za trenutni scope dodaje se samo `ProductApiBaseAddress` sa adresom `https://jsonplaceholder.typicode.com/`.
- Registruje se named `HttpClient` pod imenom `ProductApi`.
- Ime klijenta je centralizovano kroz `HttpClientNames.ProductApi`.
- `ProductService` dobija `IHttpClientFactory`, kreira named klijent i poziva relativnu putanju `users`.
- Postojeći correlation handler ostaje deo pipeline-a named klijenta i dodaje `Correlation-ID` na downstream poziv.
- Base address se validira pri pokretanju aplikacije i mora biti apsolutan URI.

## Tok konfiguracije i poziva

```text
appsettings.<Environment>.json
    ApiPaths:ProductApiBaseAddress
        -> Program.cs registruje ProductApi HttpClient
            -> ProductService kreira ProductApi klijent
                -> GET users
                    -> CorrelationIdHandler
```

## Configuration / Deployment Impact

Development vrednost je definisana u `appsettings.Development.json`. Svako drugo okruženje mora obezbediti isti ključ kroz odgovarajući environment-specific appsettings fajl, environment promenljivu ili drugi konfiguracioni provider.

## Validacija

- Solution build prolazi sa 0 grešaka i 2 ranije postojeća nullable upozorenja.
- Svi testovi prolaze (23/23).
- Dodat je test koji potvrđuje ime klijenta `ProductApi` i formiranje konačnog `.../api/users` URI-ja iz base address-a i relativne putanje.
- Dodat je test koji potvrđuje Autofac resolution `IProductService` sa registrovanim `IHttpClientFactory`.

## Dalja unapređenja

- Za svaki novi downstream API dodati zaseban naziv klijenta i zaseban `ApiPaths` ključ.
- Timeout, retry/circuit-breaker politike i autentikaciju uvoditi po konkretnom API-ju kada se pojavi poslovna potreba.

## Dopuna - više klijenata i composition root (2026-08-19)

Uveden je drugi downstream API, `HRApi`, uz sledeći dogovor:

- `Program.cs` ne poznaje pojedinačne nazive klijenata i konfiguracione ključeve;
- `Program.cs` jednim pozivom delegira registraciju HTTP klijenata u `Template.Service.DependencyInjection` composition root;
- `Template.Service.DependencyInjection` registruje konfiguracione recepte za `ProductApi` i `HRApi`, njihove base adrese i zajednički correlation handler;
- registracija recepta pri startup-u ne kreira `HttpClient` instance niti otvara mrežne konekcije;
- `ProductService` kreira oba potrebna named klijenta u svom konstruktoru, tek kada DI razreši servis;
- nije uvedeno odloženo kreiranje klijenta unutar pojedinačnih servisnih metoda;
- mrežna konekcija nastaje tek prilikom `GetAsync`/`SendAsync` poziva.

```text
Program.cs
    -> DependencyInjectionConfig.ConfigureHttpClients(...)
        -> registruje ProductApi i HRApi recepte

DI razrešava ProductService
    -> CreateClient(ProductApi)
    -> CreateClient(HRApi)

Poziv servisne metode
    -> šalje HTTP zahtev preko odgovarajućeg klijenta
```

Oba konfiguraciona ključa validiraju se kao apsolutni URI-jevi prilikom registracije. Build prolazi sa 0 grešaka i 4 postojeća nullable upozorenja u Product/Employee DTO modelima, a svi testovi prolaze (26/26).

