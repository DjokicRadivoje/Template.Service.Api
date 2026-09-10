# Service.Api.Template Application Insights Logging - Implementation Log

## Context

- Topic: Application Insights logging and correlation enrichment
- Branch: `development`
- Date: September 1, 2026
- Analysis: `docs/SERVICE_API_TEMPLATE_APPLICATION_INSIGHTS_CONFLUENCE_ANALIZA_en.md`
- Status: implemented and locally validated; deployment and Azure smoke validation pending.

## Timeline

- 2026-09-01 - Confirmed `Correlation-ID` as the canonical HTTP header; the telemetry/log property remains `CorrelationId`.
- 2026-09-01 - Added the Application Insights SDK and initially tested the AI 3.1.2 provider bridge.
- 2026-09-01 - API integration tests found an Autofac circular dependency for `OpenTelemetryLoggerProvider` with Serilog `writeToProviders: true`.
- 2026-09-01 - Verified package compatibility: Application Insights Serilog sink 5.0.1 requires the AI 2.x base package.
- 2026-09-01 - Implemented the compatible AI ASP.NET Core 2.23.0, Serilog sink 5.0.1, and Serilog 4.3.1 combination.
- 2026-09-01 - Added centralized Activity baggage and `ITelemetryInitializer` enrichment for `CorrelationId`.
- 2026-09-01 - Correlation tests passed 13/13, API tests passed 128/128, and the solution Debug build completed with zero errors.

## Implementation Notes

- `Template.Service.Api/Template.Service.Api.csproj`
  - added `Microsoft.ApplicationInsights.AspNetCore` 2.23.0;
  - added `Serilog.Sinks.ApplicationInsights` 5.0.1;
  - aligned the direct `Serilog` reference to 4.3.1.
- `Template.Service.Api/Program.cs`
  - registered `AddApplicationInsightsTelemetry()` without a connection string in code;
  - preserved the existing `UseSerilog(...)` and file sink;
  - configured the Application Insights sink to reuse the DI `TelemetryConfiguration` and `TelemetryConverter.Traces`;
  - registered `CorrelationIdTelemetryInitializer`.
- `Framework.Logger/Correlation/CorrelationIdMiddleware.cs`
  - adds the final received/generated ID to the current Activity as a `CorrelationId` tag and baggage;
  - preserves the existing request/response `Correlation-ID`, `TraceIdentifier`, and logging scope behavior.
- `Template.Service.Api/Infrastructure/Telemetry/CorrelationIdTelemetryInitializer.cs`
  - centrally adds `CorrelationId` from Activity baggage to telemetry custom properties;
  - does not overwrite a value that already exists.
- Controllers, DTOs, business logic, CRM models, and service methods were not changed.
- Request/response body logging and Authorization/Bearer/sensitive header logging were not added.

## Problems / Decisions

### AI 3.x provider bridge and Autofac

- Symptom: the API host cannot be built in integration tests because `OpenTelemetryLoggerProvider` attempts recursive creation.
- Cause: Serilog `writeToProviders: true` combined with Autofac and the AI 3.x logging provider.
- Decision: use the supported Serilog Application Insights sink with the shared `TelemetryConfiguration`.
- Compatibility: sink 5.0.1 requires Application Insights `< 3.0.0`, so stable AI ASP.NET Core 2.23.0 is pinned.

### Solution build configuration

- The first build without an explicit configuration uses the existing external `configuration-service/|Any CPU` value and fails with MSB4126.
- The repeated `--configuration Debug` build passes; this is a pre-existing environment issue, not introduced by this change.

## Validation Log

- `dotnet test Framework.Logger.Tests/Framework.Logger.Tests.csproj --no-restore` - passed, 13/13.
- `dotnet test Template.Service.Api.Tests/Template.Service.Api.Tests.csproj --no-restore` - passed, 128/128.
- `dotnet build Template.Service.Api.sln --no-restore --configuration Debug` - passed, 0 errors, 9 existing warnings.
- Restore/build still reports the existing AutoMapper 12.0.1 `NU1903`; it is outside this scope.
- Azure telemetry ingestion cannot be verified locally without deployment and remains a deployment smoke step.

## Deployment / Configuration

- Source code and `appsettings*.json` contain no Application Insights connection string.
- Every App Service/slot uses its existing `APPLICATIONINSIGHTS_CONNECTION_STRING` value.
- Before smoke testing, verify whether the Azure auto-instrumentation agent is active to prevent duplicate request/dependency records.
- Jira and Confluence were not changed.

## Commits

- Pending commit.

## Follow-up

- Deploy to DEV and inspect the `CorrelationId` custom property in `traces`, `requests`, and `dependencies`.
- Confirm the same ID for calls with and without an incoming `Correlation-ID` header.
- Confirm that there are no duplicate telemetry records, sensitive values, or full payloads.
- Repeat smoke validation on UAT and PROD slots.

## Patch / Bug Fix - Version 2 lifecycle correction

### Timeline

- 2026-09-01 - Azure and debugger validation confirmed that incoming `RequestTelemetry` is initialized before the correlation middleware.
- 2026-09-01 - DEV App Service autoinstrumentation was operationally disabled; the code-based SDK remains the authoritative telemetry path.
- 2026-09-01 - Added `ApplicationInsightsCorrelationIdMiddleware` to enrich existing request telemetry directly after correlation GUID resolution.
- 2026-09-01 - Added unit and real-host lifecycle tests for received and generated IDs.

### Implementation Notes

- `Template.Service.Api/Infrastructure/Telemetry/ApplicationInsightsCorrelationIdMiddleware.cs`
  - reads the final valid GUID from `HttpContext.TraceIdentifier`;
  - adds it to `RequestTelemetry.Properties["CorrelationId"]`;
  - preserves an existing value and safely continues when the request feature is unavailable.
- `Template.Service.Api/Program.cs`
  - registers the new middleware immediately after `UseCorrelationId()` and before authentication/authorization.
- `Template.Service.Api.Tests/Infrastructure/Telemetry/ApplicationInsightsCorrelationIdMiddlewareTests.cs`
  - covers normalization, no-overwrite, an invalid ID, and a missing telemetry feature.
- `Template.Service.Api.Tests/Infrastructure/Authentication/KeycloakAuthenticationIntegrationTests.cs`
  - the real host confirms the same received/generated GUID in request telemetry and the response header.

### Validation Log

- Initial targeted test build: failed with `CS0411` in the test-only telemetry probe because of `GetValueOrDefault` type inference; the helper was changed to explicit `TryGetValue`, after which validation was repeated and passed.
- New middleware unit tests: passed 4/4.
- New request telemetry integration tests: passed 2/2.
- `dotnet test Template.Service.Api.Tests/Template.Service.Api.Tests.csproj --no-restore --configuration Debug` - passed 138/138.
- `dotnet test Framework.Logger.Tests/Framework.Logger.Tests.csproj --no-restore --configuration Debug` - passed 13/13.
- `dotnet build Template.Service.Api.sln --no-restore --configuration Debug` - passed, 0 errors, 5 existing `NU1903` warnings for AutoMapper 12.0.1.

### Deployment / Follow-up

- The source patch has not yet been deployed; DEV KQL smoke validation remains mandatory.
- After deployment, verify the same `CorrelationId` on request, dependency, and trace telemetry and confirm there are no duplicates.
- Apply the UAT/PROD autoinstrumentation change in a controlled manner before each corresponding deployment.

