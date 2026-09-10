# Correlation ID - analiza i radni dogovor

## Status dokumenta

- Status: Implementirano i verifikovano
- Datum otvaranja: 2026-08-18
- Implementacija: Završena 2026-08-18
- Jira tiket: Nije naveden

## Kontekst i cilj

API treba da podrži jedinstveni `CorrelationId` za kompletan tok obrade jednog zahteva:

```text
Ulazni HTTP zahtev
    ↓
Middleware
    ↓
Controller → BusinessLogic → Service
    ↓
Izlazni HTTP pozivi ka drugim servisima
    ↓
HTTP odgovor klijentu
```

Ako klijent pošalje `CorrelationId`, API treba da nastavi da koristi istu vrednost. Ako vrednost nije poslata, API treba da generiše novi jedinstveni identifikator. Finalni identifikator mora da bude dostupan tokom cele obrade, prosleđen svim narednim HTTP pozivima i vraćen klijentu u HTTP odgovoru.

Correlation ID je infrastrukturni podatak. Ne treba ga prosleđivati kroz potpise controller, BusinessLogic i Service metoda, osim ako konkretan poslovni zahtev kasnije izričito zahteva drugačije.

## Početno stanje pre implementacije

Projekat već sadrži middleware `Framework.Logger/UniqueTracer/UniqueTracer.cs`, koji:

- čita zaglavlje `X-UniqueTrace-Id`;
- generiše `Guid` kada zaglavlje nije poslato;
- postavlja vrednost u `HttpContext.TraceIdentifier`;
- registrovan je u `Template.Service.Api/Program.cs` pre mapiranja kontrolera.

Postojeće rešenje trenutno nije kompletno jer:

- ne prosleđuje identifikator u izlazne `HttpClient` zahteve;
- u `Response.OnStarting` dopisuje vrednost u `Request.Headers`, umesto u `Response.Headers`;
- ne postoji potvrđena validacija ulazne vrednosti;
- nije obezbeđeno da se `CorrelationId` vidi uz svaki relevantan log;
- naziv `X-UniqueTrace-Id` nije usklađen sa dogovorenim nazivom `Correlation-ID`.

## Implementirano ponašanje

### 1. Ulazni zahtev

Middleware treba da bude jedino centralno mesto za obradu ulaznog Correlation ID zaglavlja.

Implementirani tok:

1. Pročitati HTTP zaglavlje `Correlation-ID`.
2. Ako postoji jedna vrednost u standardnom GUID formatu sa crticama (`D`), koristiti je kao finalni `CorrelationId`.
3. Ako zaglavlje ne postoji ili je vrednost prazna/nevalidna, generisati novu vrednost.
4. Postaviti finalnu vrednost u `HttpContext.TraceIdentifier`.
5. Otvoriti logging scope sa svojstvom `CorrelationId`.
6. Vratiti finalnu vrednost u response zaglavlju.

Controller ne treba ručno da čita niti generiše Correlation ID. Pošto middleware radi pre kontrolera, identifikator će već biti dostupan kada controller počne obradu.

### 2. Generisanje identifikatora

Dogovoreno je korišćenje GUID vrednosti sa crticama zbog lakše čitljivosti:

```csharp
Guid.NewGuid().ToString("D")
```

Format `D` daje standardni GUID zapis od 36 karaktera sa crticama, na primer `7f3a54c1-4205-4b8e-ae14-702de765b383`.

### 3. Izlazni HTTP pozivi

Za automatsko prosleđivanje iste vrednosti koristi se poseban `DelegatingHandler` za `HttpClient`.

Handler treba da:

- kada postoji aktivan `HttpContext`, uzme autoritativni `CorrelationId` iz `HttpContext.TraceIdentifier`;
- ukloni eventualno već postavljeno `Correlation-ID` zaglavlje sa izlaznog `HttpRequestMessage`;
- postavi vrednost iz aktivnog HTTP konteksta, tako da servisni kod ne može slučajno da prekine correlation lanac;
- kada ne postoji aktivan `HttpContext`, sačuva već postavljeno zaglavlje samo ako sadrži validan GUID u `D` formatu;
- kada ne postoji aktivan `HttpContext` niti validno eksplicitno zaglavlje, generiše novi GUID sa crticama i beleži warning.

Downstream servis na taj način dobija isti Correlation ID bez izmene potpisa BusinessLogic i Service metoda.

Postojeći default `HttpClient` registrovan je preko `IHttpClientFactory` i povezan sa correlation handlerom. Budući typed ili named clients koji pozivaju druge sisteme takođe treba da koriste isti handler.

Trenutni obim je HTTP request pipeline. Ako se kasnije uvede background job koji u okviru jednog posla pravi više downstream poziva, njegov entry point mora eksplicitno da otvori correlation scope i obezbedi jedan zajednički ID za ceo posao. U ovom koraku se ne uvodi dodatna `AsyncLocal` apstrakcija samo zbog budućeg background scenarija.

### 4. HTTP odgovor

API treba uvek da vrati finalni Correlation ID u response zaglavlju:

```text
Correlation-ID: <finalna-vrednost>
```

Ovo važi i kada je vrednost dobijena od klijenta i kada ju je API generisao. Klijent zatim može da prijavi ID uz grešku ili support zahtev.

### 5. Logovanje

Svi logovi nastali tokom obrade zahteva treba da sadrže isto strukturirano svojstvo:

```text
CorrelationId=<finalna-vrednost>
```

Middleware treba da otvori `ILogger.BeginScope` sa strukturiranim svojstvom `CorrelationId`. Postojeći Serilog već koristi `Enrich.FromLogContext`, ali file sink format treba proveriti i podesiti tako da se svojstvo zaista upisuje u fajl. Isto strukturirano svojstvo treba da bude dostupno svim budućim log sinkovima koji podržavaju scope/log context properties.

Correlation ID ne treba ručno dodavati u svaku log poruku.

Kod nevalidnog ulaznog zaglavlja prvo se generiše i postavlja novi finalni ID i otvara logging scope, a zatim se beleži warning. Tako i warning sadrži finalni `CorrelationId`. Kompletna nevalidna ulazna vrednost ne upisuje se u log.

## Correlation ID i W3C distributed tracing

Custom `Correlation-ID` i W3C `traceparent` nisu ista stvar:

- `Correlation-ID` predstavlja stabilan funkcionalni identifikator kroz ceo lanac poziva;
- `traceparent` nosi trace/span podatke za distributed tracing, pri čemu se span menja između poziva;
- oba mehanizma mogu postojati paralelno.

Implementiran je `Correlation-ID` mehanizam bez onemogućavanja standardnog `Activity`/`traceparent` ponašanja koje .NET `HttpClient` već podržava.

## Validacija i bezbednost

Ulazna vrednost dolazi iz nepouzdanog izvora i ne treba je prihvatiti bez ograničenja.

Dogovorena pravila:

- prihvatiti samo jednu vrednost zaglavlja;
- odbaciti praznu ili whitespace vrednost;
- prihvatiti isključivo GUID u standardnom formatu sa crticama (`D`);
- validaciju izvršiti pomoću `Guid.TryParseExact(value, "D", out ...)`;
- očekivana dužina validne vrednosti je tačno 36 karaktera;
- ne dozvoliti da correlation vrednost sadrži osetljive ili lične podatke;
- ne vraćati tehničke detalje o validaciji klijentu ako nisu potrebni.

Dogovoreno ponašanje za nevalidnu ulaznu vrednost je:

- nevalidna vrednost se ne prosleđuje dalje;
- generiše se novi GUID sa crticama;
- događaj se beleži kao warning bez nepotrebnog izlaganja kompletne nepoverljive vrednosti u logu;
- poslovni zahtev nastavlja obradu i ne vraća se `400 Bad Request` samo zbog nevalidnog `Correlation-ID` zaglavlja.

Na ovaj način correlation mehanizam ne prekida nepotrebno poslovni zahtev, a u logu ostaje trag da je ulazna vrednost zamenjena.

## Implementirane komponente

- `Framework.Logger/Correlation/CorrelationIdMiddleware.cs` - čitanje, validacija, generisanje, logging scope i response header.
- `Framework.Logger/Correlation/CorrelationIdHandler.cs` - prosleđivanje ID-a u izlazne HTTP pozive.
- `Framework.Logger/Correlation/CorrelationIdConstants.cs` - zajednički naziv zaglavlja, logging property i GUID format.
- `Framework.Logger/Correlation/CorrelationIdExtensions.cs` - registracija middleware-a, `IHttpContextAccessor` i handlera.
- `Template.Service.Api/Program.cs` - registracija middleware-a, `IHttpContextAccessor` i HTTP handlera/klijenata.
- `Template.Service.Api/appsettings.json` - Serilog output format koji prikazuje `CorrelationId`.
- `Framework.Logger.Tests` - middleware, outgoing handler, fallback i DI registracioni testovi.

Postojeći `UniqueTracer` zamenjen je novim correlation komponentama. Nisu ostavljena dva paralelna middleware-a koja upravljaju različitim identifikatorima za isti zahtev.

Podrška za postojeće zaglavlje `X-UniqueTrace-Id` se uklanja. Servis je još u razvoju i nema potrebe za periodom kompatibilnosti. Middleware, response i svi downstream pozivi koriste isključivo `Correlation-ID`.

## Redosled implementacije

1. Potvrditi ugovor zaglavlja, format i validaciona pravila.
2. Doraditi ili zameniti postojeći `UniqueTracer` middleware.
3. Dodati response zaglavlje i strukturirani logging scope.
4. Dodati outgoing `DelegatingHandler`.
5. Registrovati handler na svim relevantnim `HttpClient` klijentima.
6. Dodati automatske testove.
7. Proveriti log output i ručni end-to-end scenario.

## Kriterijumi prihvatanja

- [x] Kada request sadrži validan Correlation ID, ista normalizovana vrednost je dostupna u `HttpContext.TraceIdentifier`.
- [x] Kada request ne sadrži validan Correlation ID, API generiše novu jedinstvenu vrednost.
- [x] Controller i poslovni slojevi ne generišu ID i ne prosleđuju ga kroz potpise metoda.
- [x] Svi izlazni HTTP pozivi u okviru zahteva sadrže isti Correlation ID.
- [x] HTTP response sadrži finalni Correlation ID.
- [x] Relevantni strukturirani logovi sadrže svojstvo `CorrelationId`.
- [x] Paralelni zahtevi ne dele Correlation ID.
- [x] Nevalidna ili preduga ulazna vrednost obrađuje se prema dogovorenom pravilu.
- [x] Poziv bez aktivnog `HttpContext` dobija definisano fallback ponašanje.
- [x] Postojeći W3C `traceparent` mehanizam nije narušen.

## Plan validacije

Automatski testovi pokrivaju:

1. Prihvatanje validnog ulaznog ID-a.
2. Generisanje ID-a kada zaglavlje ne postoji.
3. Obradu praznog, višestrukog, predugog i nevalidnog zaglavlja.
4. Vraćanje ID-a u response zaglavlju.
5. Prosleđivanje istog ID-a kroz outgoing handler.
6. Izolaciju ID-eva kod paralelnih zahteva.
7. Fallback ponašanje kada nema aktivnog HTTP konteksta.

Ručni end-to-end scenario:

```text
1. Poslati zahtev sa poznatim `Correlation-ID` zaglavljem.
2. Proveriti response header.
3. Proveriti log aplikacije.
4. Proveriti header primljen na downstream servisu.
5. Potvrditi da je ista vrednost prisutna na sva tri mesta.
```

## Rizici

- Nepotpuna registracija handlera može dovesti do toga da samo neki downstream pozivi dobiju ID.
- Direktno kreiranje `HttpClient` instance zaobišlo bi centralni handler.
- Nekontrolisan ulazni header može zagaditi logove ili povećati njihovu veličinu.
- Staro zaglavlje `X-UniqueTrace-Id` više neće biti prihvaćeno; odluka je svesno doneta jer je servis još u razvoju.
- Tekstualni Serilog formatter mora biti podešen da prikaže `CorrelationId`; samo `Enrich.FromLogContext` ne garantuje da je svojstvo vidljivo u tekstualnom izlazu.
- Budući background posao sa više poziva mora eksplicitno da otvori zajednički correlation scope; fallback u pojedinačnom handler pozivu ne obezbeđuje automatski isti ID za više nezavisnih poziva.

## Otvorene odluke za usaglašavanje

Nema otvorenih funkcionalnih odluka koje blokiraju implementaciju.

## Rezultat implementacije i validacije

- Stari `UniqueTracer` middleware i extension su uklonjeni.
- Default `HttpClient` pipeline koristi `CorrelationIdHandler`, što je potvrđeno DI testom.
- `dotnet build Template.Service.Api.sln -c Debug --no-restore` prolazi sa 0 grešaka i 2 postojeća nullable upozorenja u product response modelima.
- `dotnet test Template.Service.Api.sln -c Debug --no-build --no-restore` prolazi: 12/12 testova.
- Lokalna runtime provera nevalidnog headera vraća HTTP 404 sa novim validnim `Correlation-ID` response headerom.
- Serilog fajl sadrži warning sa istim finalnim `CorrelationId`, bez nevalidne ulazne vrednosti.
- Build solution bez eksplicitnog `-c Debug` nije upotrebljiv u trenutnom okruženju zbog postojeće globalne vrednosti konfiguracije `configuration-service/`; eksplicitni Debug build je uspešan.

## Trenutno usaglašen pravac

- Koristi se jedan Correlation ID kroz ceo tok zahteva.
- Naziv HTTP zaglavlja je `Correlation-ID`.
- Staro zaglavlje `X-UniqueTrace-Id` se uklanja bez perioda kompatibilnosti jer je servis još u razvoju.
- Vrednost se prihvata sa ulaznog zahteva ili generiše ako nije poslata.
- Novi ID generiše se kao GUID u standardnom formatu sa crticama (`D`).
- Ulazna vrednost je validna samo ako odgovara GUID `D` formatu od 36 karaktera.
- Nevalidna ulazna vrednost zamenjuje se novim ID-em, beleži se warning i poslovni zahtev nastavlja obradu.
- Obrada je centralizovana u middleware-u, a ne u svakom kontroleru.
- Kada postoji aktivan HTTP kontekst, outgoing handler uvek prepisuje eventualni postojeći header autoritativnom vrednošću iz `HttpContext.TraceIdentifier`.
- Bez aktivnog `HttpContext` objekta handler čuva validan eksplicitni `Correlation-ID`; ako ga nema, generiše novi ID i beleži warning.
- Budući background proces sa više poziva mora eksplicitno da uspostavi jedan correlation scope za ceo posao.
- Finalni ID vraća se klijentu u response zaglavlju.
- Correlation ID je strukturirano svojstvo svih application logova unutar request scope-a i mora biti vidljiv u trenutnom Serilog file sinku.
- Warning za nevalidan ulazni ID beleži se sa novim finalnim `CorrelationId`, bez kompletne nepoverljive ulazne vrednosti.
- Ne uvodi se u potpise BusinessLogic i Service metoda.

## Granica implementiranog koraka

Implementirani obim pokriva ASP.NET HTTP request pipeline i `HttpClient` pozive registrovane preko `IHttpClientFactory`. Budući background poslovi sa više downstream poziva treba eksplicitno da otvore jedan correlation scope za ceo posao.

