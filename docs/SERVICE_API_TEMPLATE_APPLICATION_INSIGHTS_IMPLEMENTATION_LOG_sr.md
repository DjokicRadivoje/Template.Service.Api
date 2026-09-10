# Service.Api.Template Application Insights logging - Implementation Log

## Context

- Tema: Application Insights logging i correlation enrichment
- Grana: `development`
- Datum: 1. septembar 2026.
- Analysis: `docs/SERVICE_API_TEMPLATE_APPLICATION_INSIGHTS_CONFLUENCE_ANALIZA_sr.md`
- Status: implementirano i lokalno validirano; pending deployment i Azure smoke validacija.

## Timeline

- 2026-09-01 - Potvrdjen je kanonski HTTP header `Correlation-ID`; telemetry/log property ostaje `CorrelationId`.
- 2026-09-01 - Dodat je Application Insights SDK i inicijalno testiran AI 3.1.2 provider bridge.
- 2026-09-01 - API integration testovi otkrili su Autofac circular dependency za `OpenTelemetryLoggerProvider` kada se koristi Serilog `writeToProviders: true`.
- 2026-09-01 - Proverena je paket kompatibilnost: Application Insights Serilog sink 5.0.1 zahteva AI 2.x bazni paket.
- 2026-09-01 - Implementirana je kompatibilna kombinacija AI ASP.NET Core 2.23.0, Serilog sink 5.0.1 i Serilog 4.3.1.
- 2026-09-01 - Dodat je centralni Activity baggage i `ITelemetryInitializer` enrichment za `CorrelationId`.
- 2026-09-01 - Correlation testovi prosli 13/13, API testovi 128/128, a solution Debug build sa 0 gresaka.

## Implementation Notes

- `Template.Service.Api/Template.Service.Api.csproj`
  - dodat `Microsoft.ApplicationInsights.AspNetCore` 2.23.0;
  - dodat `Serilog.Sinks.ApplicationInsights` 5.0.1;
  - direktni `Serilog` uskladjen na 4.3.1.
- `Template.Service.Api/Program.cs`
  - registrovan `AddApplicationInsightsTelemetry()` bez connection stringa u kodu;
  - postojeci `UseSerilog(...)` i file sink su zadrzani;
  - Application Insights sink koristi DI `TelemetryConfiguration` i `TelemetryConverter.Traces`;
  - registrovan `CorrelationIdTelemetryInitializer`.
- `Framework.Logger/Correlation/CorrelationIdMiddleware.cs`
  - finalni received/generated ID se dodaje aktivnom `Activity` objektu kao `CorrelationId` tag i baggage;
  - postojeci request/response `Correlation-ID`, `TraceIdentifier` i logging scope ostaju nepromenjeni.
- `Template.Service.Api/Infrastructure/Telemetry/CorrelationIdTelemetryInitializer.cs`
  - centralno dodaje `CorrelationId` iz Activity baggage-ja u telemetry custom properties;
  - ne prepisuje vec postojecu vrednost.
- Nisu menjani kontroleri, DTO-i, business logika, CRM request/response modeli ili servisne metode.
- Nisu dodati request/response body logging niti Authorization/Bearer/sensitive header logging.

## Problems / Decisions

### AI 3.x provider bridge i Autofac

- Symptom: API host ne moze da se izgradi u integration testu zbog rekurzivnog kreiranja `OpenTelemetryLoggerProvider`-a.
- Cause: Serilog `writeToProviders: true` u kombinaciji sa Autofac-om i AI 3.x logging providerom.
- Decision: koristiti podrzani Serilog Application Insights sink preko zajednickog `TelemetryConfiguration` objekta.
- Compatibility: sink 5.0.1 zahteva Application Insights `< 3.0.0`, zato je pinovan stabilni AI ASP.NET Core 2.23.0.

### Solution build konfiguracija

- Prvi build bez eksplicitne konfiguracije koristi postojecu spoljnu vrednost `configuration-service/|Any CPU` i pada sa MSB4126.
- Ponovljeni `--configuration Debug` build prolazi; ovo je postojeci environment problem, nije uveden ovom izmenom.

## Validation Log

- `dotnet test Framework.Logger.Tests/Framework.Logger.Tests.csproj --no-restore` - passed, 13/13.
- `dotnet test Template.Service.Api.Tests/Template.Service.Api.Tests.csproj --no-restore` - passed, 128/128.
- `dotnet build Template.Service.Api.sln --no-restore --configuration Debug` - passed, 0 errors, 9 existing warnings.
- Restore/build i dalje prijavljuju postojeci `NU1903` za AutoMapper 12.0.1; nije deo ovog scope-a.
- Azure telemetry ingest nije lokalno proverljiv bez deploymenta; ostaje deployment smoke korak.

## Deployment / Configuration

- Source i `appsettings*.json` ne sadrze Application Insights connection string.
- Svaki App Service/slot koristi svoju postojecu `APPLICATIONINSIGHTS_CONNECTION_STRING` vrednost.
- Pre smoke testa proveriti da li je Azure auto-instrumentation agent aktivan kako bi se izbegli dupli request/dependency zapisi.
- Jira i Confluence nisu menjani.

## Commits

- Pending commit.

## Follow-up

- Deploy na DEV i proveriti `traces`, `requests` i `dependencies` custom property `CorrelationId`.
- Potvrditi isti ID za poziv sa prosledjenim i bez prosledjenog `Correlation-ID` headera.
- Potvrditi da nema duplih telemetry zapisa i da nema sensitive podataka ili punih payload-a.
- Ponoviti smoke proveru na UAT i PROD slotovima.

## Patch / Bug Fix - Version 2 lifecycle korekcija

### Timeline

- 2026-09-01 - Azure i debugger validacija potvrdile su da se incoming `RequestTelemetry` inicijalizuje pre correlation middleware-a.
- 2026-09-01 - DEV App Service auto-instrumentation je operativno iskljucena; code-based SDK ostaje autoritativna telemetry putanja.
- 2026-09-01 - Dodat je `ApplicationInsightsCorrelationIdMiddleware` koji direktno obogacuje postojeci request telemetry posle razresavanja correlation GUID-a.
- 2026-09-01 - Dodati su unit i real-host lifecycle testovi za primljeni i generisani ID.

### Implementation Notes

- `Template.Service.Api/Infrastructure/Telemetry/ApplicationInsightsCorrelationIdMiddleware.cs`
  - cita finalni validni GUID iz `HttpContext.TraceIdentifier`;
  - dodaje ga u `RequestTelemetry.Properties["CorrelationId"]`;
  - ne prepisuje postojecu vrednost i bezbedno nastavlja ako request feature nije dostupan.
- `Template.Service.Api/Program.cs`
  - novi middleware je registrovan neposredno posle `UseCorrelationId()` i pre autentikacije/autorizacije.
- `Template.Service.Api.Tests/Infrastructure/Telemetry/ApplicationInsightsCorrelationIdMiddlewareTests.cs`
  - pokriva normalizaciju, no-overwrite, invalid ID i odsutan telemetry feature.
- `Template.Service.Api.Tests/Infrastructure/Authentication/KeycloakAuthenticationIntegrationTests.cs`
  - realni host potvrdjuje isti received/generated GUID u request telemetry i response headeru.

### Validation Log

- Prvi ciljani test build: failed sa `CS0411` u test-only telemetry probe-u zbog `GetValueOrDefault` type inference-a; helper je promenjen na eksplicitni `TryGetValue`, nakon cega je validacija ponovljena i prosla.
- Novi middleware unit testovi: passed 4/4.
- Novi request telemetry integration testovi: passed 2/2.
- `dotnet test Template.Service.Api.Tests/Template.Service.Api.Tests.csproj --no-restore --configuration Debug` - passed 138/138.
- `dotnet test Framework.Logger.Tests/Framework.Logger.Tests.csproj --no-restore --configuration Debug` - passed 13/13.
- `dotnet build Template.Service.Api.sln --no-restore --configuration Debug` - passed, 0 errors, 5 postojecih `NU1903` warnings za AutoMapper 12.0.1.

### Deployment / Follow-up

- Source patch jos nije deployovan; DEV KQL smoke validacija ostaje obavezna.
- Posle deploymenta proveriti isti `CorrelationId` na request, dependency i trace telemetry i odsustvo duplikata.
- UAT/PROD auto-instrumentation promenu primeniti kontrolisano pre odgovarajuceg deploymenta.

