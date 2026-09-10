# Service.Api.Template Application Insights logging - analiza

## Izvor

- Jira: N/A
- Confluence:
  - 5. Integration Architecture (internal link removed)
  - `Service API Template Technical Overview & Standards`, odeljak `19. Logging, Correlation & Observability`
- Dodatni kontekst: Azure Application Insights end-to-end transaction prikaz za DEV okruzenje
- Datum analize: 2026-08-31
- Grana: `development`
- Status: implementirano i lokalno validirano; Azure runtime validacija pending

## Update - 2026-09-01 - usaglasen naziv correlation headera

- Potvrdjen je jedini kanonski naziv HTTP headera: `Correlation-ID`.
- Alternativni correlation header alias ne treba uvoditi u kod, konfiguraciju, testove ili novu projektnu dokumentaciju.
- Strukturirano telemetry/logging svojstvo ostaje `CorrelationId`; to je naziv custom property-ja, dok je `Correlation-ID` naziv HTTP headera.
- Application Insights refactoring mora koristiti postojecu centralnu konstantu `CorrelationIdConstants.HeaderName` umesto novih string literala.
- Ranije otvoreno pitanje izazvano Confluence nazivom smatra se zatvorenim. Jira i Confluence nisu menjani; odluka se evidentira samo u lokalnoj projektnoj dokumentaciji.

## Update - 2026-09-01 - rezultat implementacije

- Predlozeni AI 3.1.2 provider bridge (`writeToProviders: true`) je ciljano testiran, ali sa Autofac-om pravi kruznu DI zavisnost pri razresavanju `OpenTelemetryLoggerProvider`-a.
- `Serilog.Sinks.ApplicationInsights` 5.0.1 jos zahteva Application Insights 2.x bazni paket. Zato je implementirana kompatibilna stabilna kombinacija `Microsoft.ApplicationInsights.AspNetCore` 2.23.0, `Serilog.Sinks.ApplicationInsights` 5.0.1 i `Serilog` 4.3.1.
- Serilog sink koristi isti DI `TelemetryConfiguration` koji kreira `AddApplicationInsightsTelemetry()`; connection string se i dalje cita samo iz `APPLICATIONINSIGHTS_CONNECTION_STRING`.
- `CorrelationIdMiddleware` finalni dobijeni ili generisani GUID postavlja kao `CorrelationId` Activity tag i baggage. Centralni `CorrelationIdTelemetryInitializer` zatim tu vrednost dodaje request, dependency i trace custom properties.
- Kanonski HTTP header ostaje iskljucivo `Correlation-ID`; strukturirano telemetry/logging svojstvo je `CorrelationId`.
- Business logika, kontroleri, DTO-i, CRM integracija, payload-i i sensitive headeri nisu menjani niti dodatno logovani.

Ovaj implementacioni rezultat zamenjuje predlog AI 3.x provider bridge-a iz odeljka `Predlozeni Tehnicki Pristup`; ostatak scope-a i bezbednosnih ogranicenja ostaje vazeci.

## Version 2 / Patch - 2026-09-01 - Azure runtime validacija i korekcija telemetry putanje

### Razlog dopune

DEV deployment je funkcionalno potvrdio HTTP correlation ugovor, ali je otkrio da prethodna lokalna validacija nije dovoljna za Azure App Service runtime:

- bez ulaznog `Correlation-ID` headera API generise GUID i vraca ga u response headeru;
- validan ulazni GUID se vraca kao isti normalizovani `Correlation-ID`;
- nevalidan ulazni header se zamenjuje novim GUID-em;
- Serilog file sink sadrzi isti `CorrelationId` kroz controller, autentikaciju i outgoing CRM obradu;
- Application Insights `requests` i `dependencies` postoje i povezani su preko W3C `operation_Id`, ali nemaju `customDimensions["CorrelationId"]`;
- ocekivani middleware warning postoji u file sinku, ali ne postoji u Application Insights `traces`.

Azure slot istovremeno ima ukljucen App Service agent (`ApplicationInsightsAgent_EXTENSION_VERSION=~2`, mode `recommended`) i code-based Application Insights SDK. Runtime `sdkVersion` i rezultat KQL provere pokazuju da App Service agent prikuplja request/dependency telemetry, dok code-based Serilog sink i custom `ITelemetryInitializer` nisu efektivna telemetry putanja.

### Version 2 odluka

- `Service.Api.Template` koristi jednu autoritativnu, code-based Application Insights putanju.
- `APPLICATIONINSIGHTS_CONNECTION_STRING` ostaje jedini environment contract i ne upisuje se u source ili `appsettings`.
- App Service auto-instrumentation mora biti iskljucena za svaki slot koji koristi ovu verziju aplikacije; Azure promena se primenjuje odvojeno i zahteva restart slota.
- `CorrelationIdTelemetryInitializer` prvenstveno cita finalni `HttpContext.TraceIdentifier`, jer ga correlation middleware postavlja na kanonski GUID pre controller/auth/dependency obrade.
- Activity baggage ostaje fallback za telemetry nastalu bez aktivnog `HttpContext` objekta.
- Postojeca telemetry property vrednost se ne prepisuje.
- Correlation middleware ostaje nezavisan od Application Insights paketa.

### Pogodjene komponente

- `Template.Service.Api/Infrastructure/Telemetry/CorrelationIdTelemetryInitializer.cs` - pouzdano razresavanje ID-a iz aktivnog HTTP konteksta uz Activity fallback.
- `Template.Service.Api.Tests/Infrastructure/Telemetry/CorrelationIdTelemetryInitializerTests.cs` - context prioritet, fallback i no-overwrite testovi.
- `Template.Service.Api.Tests/Infrastructure/Authentication/KeycloakAuthenticationIntegrationTests.cs` - response header testovi kroz stvarni API pipeline, ukljucujuci auth short-circuit.
- Azure App Service slot konfiguracija - iskljucivanje auto-instrumentation agenta uz zadrzavanje connection stringa.

### Rizici i ogranicenja

- Dok je agent ukljucen, deployment ne moze dokazati da code-based initializer obogacuje telemetry.
- Promena Azure instrumentation moda moze privremeno prekinuti ingest ako connection string nije ispravno postavljen; zato se prvo primenjuje i proverava na DEV slotu.
- Hosting `Request starting` i `Request finished` file logovi nastaju van middleware scope-a i mogu imati prazan tekstualni `CorrelationId`; controller, auth i outgoing logovi unutar pipeline-a nose finalni ID.
- W3C `operation_Id` i poslovni `CorrelationId` ostaju odvojeni identifikatori.

### Version 2 plan validacije

1. Unit test: aktivni `HttpContext.TraceIdentifier` ima prioritet i normalizuje validan GUID.
2. Unit test: Activity baggage se koristi kada nema validnog HTTP correlation konteksta.
3. Integration test: validan ulazni header se vraca i na `401` odgovoru.
4. Integration test: bez ulaznog headera `401` odgovor sadrzi generisani GUID.
5. Lokalni API test suite i solution Debug build.
6. DEV deployment sa iskljucenim App Service auto-instrumentation agentom.
7. KQL potvrda da request, dependency i trace imaju isti `customDimensions["CorrelationId"]` i da nema duplih zapisa.

### Version 2 lifecycle korekcija i implementirani pristup

Naknadni runtime debug je pokazao da se `ITelemetryInitializer` za automatski incoming `RequestTelemetry` moze pozvati pre `CorrelationIdMiddleware`-a. U tom trenutku `HttpContext.TraceIdentifier` jos sadrzi ASP.NET Core request identifikator, a Activity baggage jos nije postavljen. Zato samo promena prioriteta izvora u initializeru ne resava incoming request telemetry.

Implementirani pristup razdvaja odgovornosti:

- novi API-specific `ApplicationInsightsCorrelationIdMiddleware` izvrsava se neposredno posle `UseCorrelationId()` i direktno dopunjuje postojeci `HttpContext.Features.Get<RequestTelemetry>()` objekat;
- u request telemetry se dodaje iskljucivo finalni, validirani GUID iz `HttpContext.TraceIdentifier`;
- postojeca `CorrelationId` property vrednost se ne prepisuje;
- `CorrelationIdTelemetryInitializer` ostaje fallback za dependency, trace, exception i telemetry bez aktivnog request feature-a;
- W3C `operation_Id` se ne menja;
- Framework correlation middleware ostaje nezavisan od Application Insights paketa.

DEV App Service auto-instrumentation je operativno iskljucena pre ovog source patch-a, dok `APPLICATIONINSIGHTS_CONNECTION_STRING` ostaje sacuvan za code-based SDK. Lokalni lifecycle test kroz stvarni API host potvrdjuje da primljeni ili generisani GUID zavrsava i u `RequestTelemetry.Properties["CorrelationId"]` i u response headeru.

## Sazetak

`Service.Api.Template` trenutno na Azure-u ima automatski prikupljene incoming request i outgoing HTTP dependency zapise. End-to-end prikaz vec pokazuje HTTP metodu, putanju, status, success/failure, trajanje i Application Insights `operation_Id`. Medjutim, aplikacioni `ILogger<T>` dogadjaji nisu vidljivi u `traces`, a poslovni `CorrelationId` nije prisutan u custom properties request, dependency i trace telemetrije.

Cilj je dodati kodno kontrolisanu Application Insights integraciju koja koristi postojeci `APPLICATIONINSIGHTS_CONNECTION_STRING` iz Azure konfiguracije, zadrzava postojeci Serilog file sink i omogucava bezbedno pracenje istog `CorrelationId` kroz incoming request, aplikacione logove i outgoing CRM pozive.

Request i response se u Application Insights-u ne modeluju kao dva odvojena zapisa. Jedan `request` ili `dependency` zapis predstavlja ceo request-response ciklus i sadrzi status, rezultat i trajanje. Puni request/response body ne treba automatski logovati.

## Dogovoreni Scope

- Dodati standardnu Application Insights telemetry registraciju u `Service.Api.Template`.
- Connection string se cita iskljucivo iz `APPLICATIONINSIGHTS_CONNECTION_STRING`.
- Ne hardkodovati DEV, UAT, PROD, slot nazive ili connection string vrednosti.
- Zadrzati postojeci Serilog setup i file sink.
- Proslediti postojece `ILogger<T>` dogadjaje u Application Insights `traces`.
- Sacuvati `CorrelationId` kao custom property aplikacionih logova.
- Dodati isti `CorrelationId` kao custom property incoming request i outgoing dependency telemetrije, ukljucujuci CRM pozive.
- Koristiti iskljucivo kanonski HTTP header `Correlation-ID`; ne uvoditi kompatibilnost sa alternativnim ili paralelnim nazivom.
- Zadrzati automatsku W3C/Application Insights korelaciju preko `operation_Id`; poslovni `CorrelationId` je dodatni identifikator, a ne zamena za `operation_Id`.
- Ne logovati Authorization/Bearer tokene, client secrets, lozinke, kompletne headere niti nemaskirane osetljive podatke.
- Ne uvoditi automatsko logovanje punih request i response body-ja.
- Ne menjati business logiku, kontrolere, DTO modele niti CRM ugovor osim ako je tehnicki neophodno za telemetry enrichment.

## Confluence Zahtevi

Odeljak `Logging, Correlation & Observability` predvidja prikupljanje:

- Correlation ID-a;
- Request ID-a;
- izvornog klijenta/sistema;
- endpointa/operacije;
- relevantnih eksternih identifikatora;
- downstream targeta;
- rezultata;
- trajanja obrade;
- stabilnog error koda.

Ne smeju se logovati access tokeni, tajne, lozinke i nemaskirani osetljivi podaci. Očekivani referentni tok je `Client -> Service API Template -> CRM API`, sa istim correlation ID-em kroz celu transakciju.

Application Insights automatska telemetry pokriva osnovne request/dependency podatke, ali source system, poslovni identifikatori i stabilni business error kod ostaju odgovornost strukturiranih aplikacionih logova.

## Trenutno Ponasanje

### Logging

- `Template.Service.Api/Program.cs` registruje Serilog preko `builder.Host.UseSerilog(...)`.
- `Template.Service.Api/appsettings.json` konfigurise dnevni file sink i prikazuje `{CorrelationId}`.
- `Framework.Logger/Correlation/CorrelationIdMiddleware.cs` postavlja finalni GUID u `HttpContext.TraceIdentifier`, request/response header i logging scope.
- Postojeci `ILogger<T>` pozivi se vide u file logu, ali na prikazanom Azure end-to-end transaction ekranu postoji `0 Traces`.

### Request i dependency telemetry

- Azure vec prikazuje incoming `REQUEST` za `Service.Api.Template`.
- Azure vec prikazuje outgoing dependencies za Keycloak i CRM.
- Request/dependency zapis sadrzi response status i duration; zaseban response telemetry zapis nije potreban.
- Application Insights `operation_Id` korelise automatsku transakciju.
- Custom property `CorrelationId` trenutno nije vidljiv.

### Paketi i konfiguracija

- `Template.Service.Api/Template.Service.Api.csproj` cilja `net10.0`.
- `Microsoft.ApplicationInsights.AspNetCore` trenutno nije referenciran.
- Azure App Service i slotovi imaju zasebne Application Insights resurse i `APPLICATIONINSIGHTS_CONNECTION_STRING` vrednosti.
- Nije potvrdjeno da li je na App Service-u aktivan auto-instrumentation agent ili je postavljen samo resource/connection string.

## Ocekivano Ponasanje

### Incoming Service.Api.Template poziv

Za svaki incoming poziv treba da postoji request telemetry sa:

- HTTP metodom i rutom;
- status kodom i success/failure rezultatom;
- ukupnim trajanjem;
- Application Insights `operation_Id`;
- custom property `CorrelationId`.

### Outgoing CRM i drugi HttpClient pozivi

Za svaki outgoing poziv treba da postoji dependency telemetry sa:

- downstream targetom;
- HTTP metodom i bezbednom putanjom;
- status kodom i success/failure rezultatom;
- trajanjem;
- istim `operation_Id` u okviru distribuirane transakcije;
- istim poslovnim `CorrelationId` kao incoming zahtev.

### Aplikacioni logovi

Postojeci pozivi:

```csharp
_logger.LogInformation(...);
_logger.LogWarning(...);
_logger.LogError(...);
```

treba da nastave da se zapisuju u postojeci Serilog file sink i da budu dostupni u Application Insights `traces`. `CorrelationId` treba da bude dostupan kao strukturirano/custom svojstvo, a ne samo deo formatirane poruke.

## Predlozeni Tehnicki Pristup

1. Dodati kompatibilan stabilan `Microsoft.ApplicationInsights.AspNetCore` paket u API projekat. Na datum analize aktuelna stabilna verzija je `3.1.2`, kompatibilna sa `net10.0`; verziju potvrditi neposredno pre implementacije.
2. Registrovati `builder.Services.AddApplicationInsightsTelemetry()` bez prosledjivanja connection stringa u kodu.
3. Zadrzati postojeci `builder.Host.UseSerilog(...)`, uz minimalnu konfiguraciju koja prosledjuje Serilog dogadjaje drugim registrovanim `ILoggerProvider` implementacijama (`writeToProviders: true`) ako ciljano testiranje potvrdi ocekivano ponasanje.
4. Omoguciti OpenTelemetry logging scopes (`IncludeScopes = true`) da bi postojeci `CorrelationId` scope bio dostupan u Application Insights logovima.
5. Dodati mali OpenTelemetry enrichment/processor ili ekvivalentno centralno resenje koje finalni `CorrelationId` dodaje incoming request i child dependency aktivnostima.
6. Ne dodavati `Serilog.Sinks.ApplicationInsights` ako provider bridge pokriva zahtev; time se izbegavaju nepotreban paket i moguca duplikacija logova.
7. Ne dodavati rucne request/dependency zapise koji dupliraju automatsku telemetry.
8. Tokom refactoringa ponovo koristiti `CorrelationIdConstants.HeaderName` za HTTP header `Correlation-ID`; telemetry property `CorrelationId` drzati odvojeno preko `CorrelationIdConstants.LoggingPropertyName` ili jednako centralizovanog resenja.

Tacna processor implementacija treba da se potvrdi ciljanim testom za ASP.NET Core i `HttpClient` aktivnosti. Resenje mora ostati centralno i ne sme zahtevati izmene svake service metode.

## Azure / Deployment Granica

Funkcionalna implementacija je u kodu. Azure konfiguracija ostaje izvor destination connection stringa.

Potrebno je proveriti:

- da svaki DEV/UAT/PROD App Service ili slot ima odgovarajuci `APPLICATIONINSIGHTS_CONNECTION_STRING`;
- da su slot-specific vrednosti oznacene kao deployment slot settings kada je potrebno;
- da li postoje `ApplicationInsightsAgent_EXTENSION_VERSION`, `APPLICATIONINSIGHTS_ENABLE_AGENT` ili povezane auto-instrumentation postavke;
- da se ne aktiviraju dva paralelna mehanizma instrumentacije;
- da nakon deploymenta nema duplih request, dependency ili trace zapisa.

Ako je Azure auto-instrumentation agent aktivan, treba koordinisano odluciti da kodni SDK postane autoritativan mehanizam i po potrebi iskljuciti agent. Application Insights resursi i connection stringovi ostaju vazeci.

## Pogodjene Komponente

| Komponenta | Fajl/Klasa | Ocekivani uticaj |
|---|---|---|
| API paket | `Template.Service.Api/Template.Service.Api.csproj` | Application Insights SDK referenca |
| Startup | `Template.Service.Api/Program.cs` | Telemetry registracija, Serilog provider bridge i scope konfiguracija |
| Correlation infrastruktura | `Framework.Logger/Correlation/CorrelationIdConstants.cs`, `CorrelationIdMiddleware.cs` ili nova centralna telemetry komponenta | Kanonski `Correlation-ID` header i enrichment request/dependency aktivnosti poslovnim `CorrelationId` svojstvom |
| Logging konfiguracija | `Template.Service.Api/appsettings.json` | Postojeci file sink ostaje; menjati samo ako je potreban provider log-level filter, bez connection stringa |
| Testovi | `Framework.Logger.Tests` i/ili `Template.Service.Api.Tests` | Provera scope/enrichment i odsustva duplikacije |

## Acceptance Criteria

- [x] API se build-uje sa Application Insights SDK-om na `net10.0`.
- [x] Aplikacija cita destination iskljucivo iz `APPLICATIONINSIGHTS_CONNECTION_STRING`.
- [x] Postojeci Serilog file sink i konfiguracija ostaju aktivni.
- [ ] `LogInformation`, `LogWarning` i `LogError` dogadjaji pojavljuju se u Application Insights `traces` prema konfigurisanom nivou i sampling pravilima.
- [ ] `CorrelationId` je strukturirano svojstvo relevantnih trace zapisa.
- [ ] Incoming request telemetry sadrzi `CorrelationId` kao custom property.
- [ ] Outgoing CRM dependency telemetry sadrzi isti `CorrelationId` kao incoming zahtev.
- [x] Svi incoming, response i outgoing HTTP headeri koriste kanonski naziv `Correlation-ID` preko centralne konstante.
- [x] Refactoring ne uvodi alternativni alias niti novi correlation header string literal.
- [ ] Incoming request i outgoing dependencies dele ocekivani Application Insights `operation_Id` tok.
- [ ] Request/dependency telemetry prikazuje metod, putanju/target, status, rezultat i duration.
- [ ] Ne postoje dupli request/dependency/trace zapisi zbog paralelne instrumentacije.
- [x] Authorization header, tokeni, secrets, lozinke i puni osetljivi payload-i nisu uvedeni u logovanje.
- [x] Nema environment-specific vrednosti u source kodu ili `appsettings*.json` fajlovima.
- [x] Business logika, kontroleri, DTO modeli i CRM ugovor ostaju neizmenjeni.

## Plan Validacije

- [x] `dotnet build Template.Service.Api.sln --no-restore --configuration Debug` - 0 gresaka, 9 postojecih warning-a.
- [x] Pokrenuti ciljane i kompletne testove correlation i API startup/DI konfiguracije - 13/13 i 128/128 passed.
- [ ] Lokalno ili u izolovanom test okruzenju potvrditi da file sink i Application Insights provider primaju isti `ILogger<T>` dogadjaj bez dupliranja.
- [ ] Poslati zahtev sa poznatim `Correlation-ID` headerom i potvrditi istu vrednost u `traces`, incoming request i CRM dependency custom properties.
- [ ] Poslati zahtev bez `Correlation-ID` headera i potvrditi da je generisana vrednost ista kroz sva tri telemetry tipa.
- [ ] Potvrditi da response i svi outgoing servisni pozivi koriste iskljucivo header `Correlation-ID`.
- [ ] Potvrditi request status/duration i CRM dependency status/duration.
- [ ] Proveriti Azure `End-to-end transaction details`: jedan request, ocekivane dependencies i povezani traces.
- [ ] Proveriti da nema Authorization/Bearer tokena niti request/response body-ja u telemetry podacima.
- [ ] Ponoviti smoke proveru na DEV, UAT i PROD resursima bez uporedjivanja ili objavljivanja connection string vrednosti.

## Rizici i Otvorena Pitanja

| # | Stavka | Predlog / sledeci korak |
|---|---|---|
| 1 | Nije potvrdjeno da li Azure App Service koristi auto-instrumentation agent. | Pre deploymenta proveriti App Service environment settings i izabrati jedan autoritativni instrumentation mehanizam. |
| 2 | Serilog provider bridge sa AI 3.x i Autofac-om pravi kruznu DI zavisnost. | Reseno kompatibilnim AI 2.23 SDK-om i standardnim Serilog sinkom preko zajednickog `TelemetryConfiguration`. |
| 3 | Logging scope sam ne garantuje request/dependency custom properties. | Reseno centralnim Activity baggage + `ITelemetryInitializer` enrichmentom i ciljanim testovima. |
| 4 | Application Insights koristi sopstveni W3C `operation_Id`, odvojen od poslovnog `CorrelationId`. | Zadrzati oba identifikatora i dokumentovati njihovu namenu. |
| 5 | Application Insights sampling moze uticati na vidljivost pojedinacnih zapisa. | Prvo koristiti podrazumevano ponasanje, zatim proceniti volumen, trosak i potrebnu sampling politiku. |
| 6 | Logovanje punih payload-a bi povecalo bezbednosni i troskovni rizik. | Ograniciti se na metadata i eksplicitno odobrene, bezbedne poslovne identifikatore. |
| 7 | Naziv headera u eksternoj specifikaciji nije bio uskladjen sa lokalnim projektom. | Zatvoreno 2026-09-01: kanonski naziv je `Correlation-ID`; eksterni Atlassian sadrzaj se ne menja u okviru ovog rada. |

## Van Scope-a Implementacije
- Promena Azure resursa ili connection string vrednosti.
- Logovanje kompletnih request/response body-ja.
- Uvodjenje dashboarda, alert pravila ili finalne sampling politike.
- Uvodjenje dodatnog correlation header aliasa; kanonski naziv je vec usaglasen kao `Correlation-ID`.



