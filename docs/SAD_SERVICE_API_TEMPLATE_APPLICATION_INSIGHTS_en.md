# Service.Api.Template Application Insights Logging

## Patch / Bug Fix - 2026-09-01 - RequestTelemetry lifecycle

- Reason: the incoming-request `ITelemetryInitializer` can run before the correlation middleware, before the final GUID exists.
- Previous behavior: the HTTP response header and file logs contain `CorrelationId`, but the Application Insights request custom property can remain empty.
- New behavior: an API-specific middleware registered immediately after `UseCorrelationId()` directly adds the final GUID to the existing `RequestTelemetry.Properties["CorrelationId"]`.
- `CorrelationIdTelemetryInitializer` remains the fallback for dependency, trace, and other telemetry; W3C `operation_Id` is unchanged.
- DEV App Service autoinstrumentation is disabled, and the code-based SDK uses the existing environment connection string.
- Validation: middleware 4/4, request telemetry integration 2/2, API 138/138, Framework.Logger 13/13, solution build with 0 errors.
- Remaining: DEV deployment and KQL smoke validation for request/dependency/trace telemetry and duplicate absence.

## Summary

`Service.Api.Template` now registers Application Insights telemetry, preserves the existing Serilog file sink, and sends `ILogger<T>` events to Application Insights `traces`. Incoming request, outgoing `HttpClient` dependency, and trace telemetry can carry the same structured `CorrelationId` property.

## Implemented Changes

- Added Application Insights ASP.NET Core SDK 2.23.0.
- Added Serilog Application Insights sink 5.0.1 using the shared DI `TelemetryConfiguration`.
- `AddApplicationInsightsTelemetry()` automatically collects requests, dependencies, and exceptions.
- Correlation middleware adds the final received/generated GUID as an Activity `CorrelationId` tag and baggage value.
- A centralized telemetry initializer adds `CorrelationId` to telemetry custom properties.
- The only canonical HTTP header is `Correlation-ID`; the telemetry/log property is `CorrelationId`.

## Affected Components

- `Template.Service.Api/Template.Service.Api.csproj` - telemetry/logging packages.
- `Template.Service.Api/Program.cs` - Application Insights, Serilog sink, and initializer registration.
- `Framework.Logger/Correlation/CorrelationIdMiddleware.cs` - Activity tag/baggage enrichment.
- `Template.Service.Api/Infrastructure/Telemetry/CorrelationIdTelemetryInitializer.cs` - telemetry custom property enrichment.
- Relevant test projects - correlation and API host/DI validation.

## Configuration and Deployment

- The connection string is not present in source code or `appsettings*.json`.
- Each Azure App Service/slot uses its own `APPLICATIONINSIGHTS_CONNECTION_STRING`.
- Check the Azure auto-instrumentation agent before deployment to prevent duplicate request/dependency telemetry.
- There is no DEV/UAT/PROD branching in code.

## Security

No request/response body logging, Authorization/Bearer tokens, secrets, passwords, or complete sensitive headers were added. Business logic, controllers, DTOs, and CRM integration remain unchanged.

## Validation

- Framework.Logger tests: 13/13 passed.
- Template.Service.Api tests: 128/128 passed.
- Solution Debug build: passed, 0 errors, 9 existing warnings.
- Azure smoke validation: pending deployment.

## Azure Verification

After deployment, send one request with a known `Correlation-ID` and one without the header. In Application Insights `Transaction search` / `End-to-end transaction details`, inspect the request, CRM dependency, and related traces. All three telemetry types should contain the same `CorrelationId` in custom properties. A request/dependency record already represents the request-response cycle and should show status and duration; a full body is not expected.

