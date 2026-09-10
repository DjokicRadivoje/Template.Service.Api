# Service.Api.Template Application Insights logging

## Patch / Bug Fix - 2026-09-01 - RequestTelemetry lifecycle

- Razlog: `ITelemetryInitializer` za incoming request moze biti pozvan pre correlation middleware-a i tada jos nema finalni GUID.
- Prethodno ponasanje: HTTP response header i file logovi imaju `CorrelationId`, ali Application Insights request custom property moze ostati prazna.
- Novo ponasanje: API-specific middleware, registrovan odmah posle `UseCorrelationId()`, direktno dodaje finalni GUID u postojeci `RequestTelemetry.Properties["CorrelationId"]`.
- `CorrelationIdTelemetryInitializer` ostaje fallback za dependency, trace i ostalu telemetry; W3C `operation_Id` se ne menja.
- DEV App Service auto-instrumentation je iskljucena, a code-based SDK koristi postojeci environment connection string.
- Validacija: middleware 4/4, request telemetry integration 2/2, API 138/138, Framework.Logger 13/13, solution build 0 gresaka.
- Preostalo: DEV deployment i KQL smoke potvrda za request/dependency/trace i odsustvo duplikata.

## Sazetak

`Service.Api.Template` sada registruje Application Insights telemetry, zadrzava postojeci Serilog file sink i salje `ILogger<T>` dogadjaje u Application Insights `traces`. Incoming request, outgoing `HttpClient` dependency i trace telemetry mogu da nose isto strukturirano `CorrelationId` svojstvo.

## Implementirane Izmene

- Dodat je Application Insights ASP.NET Core SDK 2.23.0.
- Dodat je Serilog Application Insights sink 5.0.1 koji koristi zajednicki DI `TelemetryConfiguration`.
- `AddApplicationInsightsTelemetry()` automatski prikuplja requests, dependencies i exceptions.
- Correlation middleware finalni received/generated GUID dodaje kao Activity `CorrelationId` tag i baggage.
- Centralni telemetry initializer dodaje `CorrelationId` u telemetry custom properties.
- Kanonski HTTP header je iskljucivo `Correlation-ID`; telemetry/log property je `CorrelationId`.

## Pogodjene Komponente

- `Template.Service.Api/Template.Service.Api.csproj` - telemetry/logging paketi.
- `Template.Service.Api/Program.cs` - Application Insights, Serilog sink i initializer registracija.
- `Framework.Logger/Correlation/CorrelationIdMiddleware.cs` - Activity tag/baggage enrichment.
- `Template.Service.Api/Infrastructure/Telemetry/CorrelationIdTelemetryInitializer.cs` - telemetry custom property enrichment.
- Relevantni test projekti - correlation i API host/DI validacija.

## Konfiguracija i Deployment

- Connection string nije u source kodu niti u `appsettings*.json`.
- Svaki Azure App Service/slot koristi sopstveni `APPLICATIONINSIGHTS_CONNECTION_STRING`.
- Pre deploymenta treba proveriti Azure auto-instrumentation agent da ne bi postojala dupla request/dependency telemetry.
- Nema DEV/UAT/PROD grananja u kodu.

## Bezbednost

Nisu dodati request/response body logging, Authorization/Bearer tokeni, secrets, lozinke ili kompletni sensitive headeri. Business logika, kontroleri, DTO-i i CRM integracija nisu menjani.

## Validacija

- Framework.Logger testovi: 13/13 passed.
- Template.Service.Api testovi: 128/128 passed.
- Solution Debug build: passed, 0 errors, 9 postojecih warning-a.
- Azure smoke validacija: pending deployment.

## Azure Provera

Nakon deploymenta poslati jedan zahtev sa poznatim `Correlation-ID` i jedan bez headera. U Application Insights `Transaction search` / `End-to-end transaction details` proveriti request, CRM dependency i related traces. U custom properties sva tri tipa treba da imaju isti `CorrelationId`. Request/dependency zapis vec predstavlja request-response ciklus i treba da prikaze status i duration; puni body se ne ocekuje.

