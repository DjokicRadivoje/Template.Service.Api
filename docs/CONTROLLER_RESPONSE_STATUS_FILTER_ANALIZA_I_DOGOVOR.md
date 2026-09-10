# Centralizovano Response HTTP status mapiranje - analiza i dogovor

## Status dokumenta

- Status: Implementirano i verifikovano
- Datum: 2026-08-18
- Jira tiket: Nije naveden

## Problem

`ProductController.GetProduct` je pored poziva BusinessLogic sloja sadržao grananje nad `Response<T>.Success` i message codes radi izbora HTTP 200, 500, 502 ili 504 statusa. Ista logika bi morala da se kopira u svaki novi controller.

Cilj je da controller ostane bez status-mapping logike:

```csharp
return Ok(await _productBusinessLogic.GetProduct(
    request,
    cancellationToken));
```

uz očuvanje postojećeg HTTP ponašanja.

## Usaglašene odluke

- Postojeće mapiranje statusa ostaje nepromenjeno.
- Mapiranje se primenjuje globalno na svaki standardni `Response<T>`.
- HTTP status je presentation/API odgovornost i ne prebacuje se u BusinessLogic.
- Eksplicitni non-200 i nestandardni rezultati ne smeju biti prepisani.
- Automatski model-validation 400 ostaje van trenutnog scope-a.

## Implementirano rešenje

### IResponse ugovor

`Response<T>` implementira negenerički `IResponse`, koji filteru izlaže samo:

- `Success`;
- read-only kolekciju `Messages`.

Generički payload ostaje nebitan za izbor HTTP statusa, a filter ne koristi reflection.

### Status resolver

`ResponseHttpStatusCodeResolver` sadrži centralnu politiku:

| Uslov | HTTP status |
|---|---:|
| `Success == true` | 200 |
| postoji `BUSINESS_LOGIC_ERROR` | 500 |
| postoji `SERVICE_TIMEOUT` | 504 |
| ostala standardna greška | 502 |

Redosled čuva prethodni prioritet: business error ima prednost nad timeout-om.

### Globalni MVC result filter

`ResponseHttpStatusFilter`:

- obrađuje `ObjectResult` čija vrednost implementira `IResponse`;
- menja status samo kada je inicijalni status 200 ili nije eksplicitno postavljen;
- ostavlja response body nepromenjen;
- ne utiče na eksplicitni non-200, file, redirect ili nestandardni DTO rezultat.

Filter je globalno registrovan kroz MVC options.

## Affected Components

- `Template.Service.BusinessModel/Common/IResponse.cs` - negenerički response ugovor.
- `Template.Service.BusinessModel/Common/Response.cs` - implementira `IResponse`.
- `Template.Service.Api/Infrastructure/Http/IResponseHttpStatusCodeResolver.cs` - resolver ugovor.
- `Template.Service.Api/Infrastructure/Http/ResponseHttpStatusCodeResolver.cs` - status politika.
- `Template.Service.Api/Infrastructure/Http/ResponseHttpStatusFilter.cs` - globalno result mapiranje.
- `Template.Service.Api/Program.cs` - resolver i filter registracija.
- `Template.Service.Api/Controllers/ProductController.cs` - samo BusinessLogic poziv i `Ok` rezultat.
- `Template.Service.Api.Tests` - resolver, filter i controller testovi.

## Kriterijumi prihvatanja

- [x] Controller ne sadrži message-code/status grananje.
- [x] Uspešan `Response<T>` daje HTTP 200.
- [x] `BUSINESS_LOGIC_ERROR` daje HTTP 500.
- [x] `SERVICE_TIMEOUT` daje HTTP 504.
- [x] Ostala standardna greška daje HTTP 502.
- [x] Business error ima prioritet nad timeout-om.
- [x] Eksplicitni non-200 rezultat nije prepisan.
- [x] Nestandardni response nije promenjen.
- [x] Response body ostaje isti objekat.
- [x] Filter je globalno registrovan.

## Validacija

- `dotnet build Template.Service.Api.sln -c Debug --no-restore` - passed, 0 errors, 0 warnings.
- `dotnet test Template.Service.Api.sln -c Debug --no-build --no-restore` - passed, 21/21 testova ukupno.
- Novi `Template.Service.Api.Tests` - 9/9 testova.
- Lokalni runtime POST `/Product` - HTTP 200, standardni JSON response i očekivani `Correlation-ID`.
- Automatski model-validation 400 je potvrđen kao pre-controller scenario i nije promenjen.

## Otvorene teme van scope-a

- Preciznije buduće mapiranje `SERVICE_UNAVAILABLE` na 503.
- Standardizacija automatskog model-validation 400 odgovora.
- OpenAPI konvencije za centralno mapirane statuse.

## Zaključak

Status mapping je centralizovan u API presentation infrastrukturi. BusinessLogic ostaje nezavisan od HTTP statusa, a controller je sveden na delegiranje i vraćanje standardnog rezultata.

