# Correlation ID - Implementation Log

## Context

- Ticket: Nije naveden
- Branch: `main`
- Started: 2026-08-18
- Main document: `docs/CORRELATION_ID_ANALIZA_I_DOGOVOR.md`

## Timeline

- 2026-08-18 - Usaglašen `Correlation-ID` ugovor, GUID `D` format, invalid-input fallback, outgoing propagation i logging pravila.
- 2026-08-18 - Stari `UniqueTracer` zamenjen correlation middleware-om i handlerom.
- 2026-08-18 - Default `HttpClient` registrovan sa correlation handlerom i podešen Serilog output format.
- 2026-08-18 - Dodat `Framework.Logger.Tests` projekat sa 12 testova.
- 2026-08-18 - Izvršeni solution build, testovi i lokalna runtime/log provera.
- 2026-08-18 - Dokumentacija arhivirana u lokalni `History/correlation-id` folder.

## Implementation Notes

- `Framework.Logger/Correlation/CorrelationIdMiddleware.cs` - validira ulazni `Correlation-ID`, generiše fallback, postavlja request/response/trace vrednost i otvara logging scope.
- `Framework.Logger/Correlation/CorrelationIdHandler.cs` - autoritativno propagira request correlation ID i definiše fallback bez `HttpContext` objekta.
- `Framework.Logger/Correlation/CorrelationIdConstants.cs` - centralizuje header, logging property i GUID format.
- `Framework.Logger/Correlation/CorrelationIdExtensions.cs` - registruje accessor, handler i middleware.
- `Framework.Logger/Framework.Logger.csproj` - koristi `Microsoft.AspNetCore.App` framework reference umesto starih/mismatched abstractions paketa.
- `Template.Service.Api/Program.cs` - povezuje middleware i default `HttpClient` pipeline.
- `Template.Service.Api/appsettings.json` - prikazuje `CorrelationId` u tekstualnom file sinku.
- `Framework.Logger.Tests` - pokriva middleware, handler, DI registraciju, parallel request izolaciju i fallback scenarije.

## Problems / Bugs

### Middleware definite assignment

- Symptom: Prvi API build je pao sa `CS0165` za `parsedCorrelationId`.
- Cause: `out var` deklaracija nalazila se unutar short-circuit izraza.
- Fix: Promenljiva je eksplicitno inicijalizovana pre validacionog izraza.
- Status: Fixed and verified.

### Empty-string test assertion

- Symptom: Prvi test run je imao 1 neuspešan test od 11.
- Cause: Test je pozivao `DoesNotContain` sa praznim stringom, koji je sadržan u svakom tekstu.
- Fix: Provera neizlaganja ulazne vrednosti izvršava se samo za neprazan invalid input.
- Status: Fixed and verified; finalno 12/12 testova prolazi.

### Solution configuration from environment

- Symptom: `dotnet build Template.Service.Api.sln --no-restore` prijavio je nevažeću konfiguraciju `configuration-service/|Any CPU`.
- Cause: Postojeća globalna/environment konfiguracija, nevezana za correlation izmenu.
- Resolution: Validacija je pokrenuta sa eksplicitnim `-c Debug`.
- Status: Correlation implementacija verifikovana; postojeća environment specifičnost ostaje van ovog obima.

## Validation Log

- `dotnet build Template.Service.Api/Template.Service.Api.csproj --no-restore` - passed nakon lokalne CS0165 korekcije.
- `dotnet test Framework.Logger.Tests/Framework.Logger.Tests.csproj --no-restore` - passed, 12/12.
- `dotnet build Template.Service.Api.sln -c Debug --no-restore` - passed, 0 errors, 2 existing nullable warnings.
- `dotnet test Template.Service.Api.sln -c Debug --no-build --no-restore` - passed, 12/12.
- Runtime HTTP 404 sa nevalidnim headerom - response sadrži nov validan `Correlation-ID`.
- Serilog runtime provera - warning sadrži isti finalni `CorrelationId`, bez invalid input vrednosti.

## Commits

- Pending commit.

## Deployment / Environment

- Dev: Nije deployovano.
- Prod: Nije deployovano.
- Configuration: Nema novih konfiguracionih ključeva; promenjen je Serilog file output template.
- History: Osvežen `C:\Users\Radivoje\.codex\skills\History\correlation-id`.

## Follow-up

- Ako se uvedu background poslovi sa više downstream poziva, njihov entry point treba da uspostavi zajednički correlation scope.

