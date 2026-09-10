# Service.Api.Template Application Insights logging

## Patch / Bug Fix - 2026-09-01 - RequestTelemetry lifecycle

- Причина: `ITelemetryInitializer` за влезното барање може да се изврши пред correlation middleware-от, пред да постои финалниот GUID.
- Претходно однесување: HTTP response header-от и file логовите имаат `CorrelationId`, но Application Insights request custom property може да остане празна.
- Ново однесување: API-specific middleware, регистриран веднаш по `UseCorrelationId()`, директно го додава финалниот GUID во постојниот `RequestTelemetry.Properties["CorrelationId"]`.
- `CorrelationIdTelemetryInitializer` останува fallback за dependency, trace и останатата telemetry; W3C `operation_Id` не се менува.
- DEV App Service auto-instrumentation е исклучена, а code-based SDK го користи постојниот environment connection string.
- Валидација: middleware 4/4, request telemetry integration 2/2, API 138/138, Framework.Logger 13/13, solution build со 0 грешки.
- Преостанува: DEV deployment и KQL smoke проверка за request/dependency/trace telemetry и отсуство на дупликати.

## Резиме

`Service.Api.Template` сега регистрира Application Insights telemetry, го задржува постоечкиот Serilog file sink и ги испраќа `ILogger<T>` настаните во Application Insights `traces`. Incoming request, outgoing `HttpClient` dependency и trace telemetry можат да го носат истото структурирано својство `CorrelationId`.

## Имплементирани Измени

- Додаден е Application Insights ASP.NET Core SDK 2.23.0.
- Додаден е Serilog Application Insights sink 5.0.1 кој го користи заедничкиот DI `TelemetryConfiguration`.
- `AddApplicationInsightsTelemetry()` автоматски собира requests, dependencies и exceptions.
- Correlation middleware го додава финалниот примен/генериран GUID како Activity `CorrelationId` tag и baggage.
- Централниот telemetry initializer го додава `CorrelationId` во telemetry custom properties.
- Единствениот канонски HTTP header е `Correlation-ID`; telemetry/log property е `CorrelationId`.

## Засегнати Компоненти

- `Template.Service.Api/Template.Service.Api.csproj` - telemetry/logging пакети.
- `Template.Service.Api/Program.cs` - Application Insights, Serilog sink и initializer регистрација.
- `Framework.Logger/Correlation/CorrelationIdMiddleware.cs` - Activity tag/baggage enrichment.
- `Template.Service.Api/Infrastructure/Telemetry/CorrelationIdTelemetryInitializer.cs` - telemetry custom property enrichment.
- Релевантните test проекти - correlation и API host/DI валидација.

## Конфигурација и Deployment

- Connection string не е во source code ниту во `appsettings*.json`.
- Секој Azure App Service/slot ја користи сопствената `APPLICATIONINSIGHTS_CONNECTION_STRING` вредност.
- Пред deployment треба да се провери Azure auto-instrumentation agent за да нема дуплирана request/dependency telemetry.
- Нема DEV/UAT/PROD разгранување во кодот.

## Безбедност

Не се додадени request/response body logging, Authorization/Bearer токени, secrets, лозинки или целосни sensitive headers. Business logic, controllers, DTO модели и CRM integration не се изменети.

## Валидација

- Framework.Logger тестови: 13/13 passed.
- Template.Service.Api тестови: 128/128 passed.
- Solution Debug build: passed, 0 errors, 9 постоечки warnings.
- Azure smoke валидација: pending deployment.

## Azure Проверка

По deployment да се испрати едно барање со познат `Correlation-ID` и едно без header. Во Application Insights `Transaction search` / `End-to-end transaction details` да се проверат request, CRM dependency и related traces. Во custom properties сите три типа треба да го имаат истиот `CorrelationId`. Request/dependency записот веќе го претставува request-response циклусот и треба да прикажува status и duration; целосен body не се очекува.

