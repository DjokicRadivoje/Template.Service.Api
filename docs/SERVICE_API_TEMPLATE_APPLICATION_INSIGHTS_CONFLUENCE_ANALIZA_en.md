# Service.Api.Template Application Insights Logging - Analysis

## Source

- Jira: N/A
- Confluence:
  - 5. Integration Architecture (internal link removed)
  - `Service API Template Technical Overview & Standards`, section `19. Logging, Correlation & Observability`
- Additional context: Azure Application Insights end-to-end transaction view for the DEV environment
- Analysis date: 2026-08-31
- Branch: `development`
- Status: implemented and locally validated; Azure runtime validation pending

## Update - 2026-09-01 - correlation header name aligned

- The only canonical HTTP header name has been confirmed as `Correlation-ID`.
- An alternative correlation header alias must not be introduced in code, configuration, tests, or new project documentation.
- The structured telemetry/logging property remains `CorrelationId`; this is the custom property name, while `Correlation-ID` is the HTTP header name.
- The Application Insights refactoring must reuse the existing `CorrelationIdConstants.HeaderName` constant instead of adding new string literals.
- The earlier open question caused by the Confluence wording is considered closed. Jira and Confluence were not changed; the decision is recorded only in local project documentation.

## Update - 2026-09-01 - implementation result

- The proposed AI 3.1.2 provider bridge (`writeToProviders: true`) was tested, but with Autofac it creates a circular DI dependency while resolving `OpenTelemetryLoggerProvider`.
- `Serilog.Sinks.ApplicationInsights` 5.0.1 still requires the Application Insights 2.x base package. The implemented compatible stable combination is therefore `Microsoft.ApplicationInsights.AspNetCore` 2.23.0, `Serilog.Sinks.ApplicationInsights` 5.0.1, and `Serilog` 4.3.1.
- The Serilog sink reuses the DI `TelemetryConfiguration` created by `AddApplicationInsightsTelemetry()`; the connection string is still read only from `APPLICATIONINSIGHTS_CONNECTION_STRING`.
- `CorrelationIdMiddleware` sets the final received or generated GUID as an Activity `CorrelationId` tag and baggage value. The centralized `CorrelationIdTelemetryInitializer` then adds it to request, dependency, and trace custom properties.
- The only canonical HTTP header remains `Correlation-ID`; the structured telemetry/logging property is `CorrelationId`.
- Business logic, controllers, DTOs, CRM integration, payloads, and sensitive headers were not changed or additionally logged.

This implementation result supersedes the proposed AI 3.x provider bridge in `Proposed Technical Approach`; the remaining scope and security constraints are unchanged.

## Version 2 / Patch - 2026-09-01 - Azure runtime validation and telemetry-path correction

### Reason for the update

The DEV deployment functionally confirmed the HTTP correlation contract but showed that the previous local validation was not sufficient for the Azure App Service runtime:

- when the incoming `Correlation-ID` header is absent, the API generates a GUID and returns it in the response header;
- a valid incoming GUID is returned as the same normalized `Correlation-ID`;
- an invalid incoming header is replaced with a new GUID;
- the Serilog file sink contains the same `CorrelationId` throughout controller, authentication, and outgoing CRM processing;
- Application Insights `requests` and `dependencies` exist and are linked by the W3C `operation_Id`, but do not contain `customDimensions["CorrelationId"]`;
- the expected middleware warning exists in the file sink but is absent from Application Insights `traces`.

The Azure slot simultaneously has the App Service agent enabled (`ApplicationInsightsAgent_EXTENSION_VERSION=~2`, mode `recommended`) and the code-based Application Insights SDK. Runtime `sdkVersion` values and KQL results show that the App Service agent collects request/dependency telemetry while the code-based Serilog sink and custom `ITelemetryInitializer` are not the effective telemetry path.

### Version 2 decision

- `Service.Api.Template` uses one authoritative code-based Application Insights path.
- `APPLICATIONINSIGHTS_CONNECTION_STRING` remains the only environment contract and is not stored in source code or `appsettings`.
- App Service autoinstrumentation must be disabled for every slot running this application version; the Azure change is applied separately and requires a slot restart.
- `CorrelationIdTelemetryInitializer` primarily reads the final `HttpContext.TraceIdentifier`, which the correlation middleware sets to the canonical GUID before controller/authentication/dependency processing.
- Activity baggage remains the fallback for telemetry created without an active `HttpContext`.
- An existing telemetry property is never overwritten.
- The correlation middleware remains independent of Application Insights packages.

### Affected components

- `Template.Service.Api/Infrastructure/Telemetry/CorrelationIdTelemetryInitializer.cs` - reliable resolution from the active HTTP context with an Activity fallback.
- `Template.Service.Api.Tests/Infrastructure/Telemetry/CorrelationIdTelemetryInitializerTests.cs` - context priority, fallback, and no-overwrite tests.
- `Template.Service.Api.Tests/Infrastructure/Authentication/KeycloakAuthenticationIntegrationTests.cs` - response-header tests through the real API pipeline, including an authentication short-circuit.
- Azure App Service slot configuration - disable the autoinstrumentation agent while retaining the connection string.

### Risks and limitations

- While the agent remains enabled, a deployment cannot prove that the code-based initializer enriches telemetry.
- Changing the Azure instrumentation mode can temporarily interrupt ingestion if the connection string is not configured correctly, so the change must be applied and verified on DEV first.
- Hosting `Request starting` and `Request finished` file logs are emitted outside the middleware scope and can have an empty textual `CorrelationId`; controller, authentication, and outgoing logs inside the pipeline carry the final ID.
- W3C `operation_Id` and the business `CorrelationId` remain separate identifiers.

### Version 2 validation plan

1. Unit test: active `HttpContext.TraceIdentifier` takes precedence and normalizes a valid GUID.
2. Unit test: Activity baggage is used when there is no valid HTTP correlation context.
3. Integration test: a valid incoming header is returned on a `401` response.
4. Integration test: without an incoming header, a `401` response contains a generated GUID.
5. Run the local API test suite and solution Debug build.
6. Deploy to DEV with the App Service autoinstrumentation agent disabled.
7. Use KQL to confirm that request, dependency, and trace telemetry share `customDimensions["CorrelationId"]` and that no duplicate records exist.

### Version 2 lifecycle correction and implemented approach

Follow-up runtime debugging showed that the `ITelemetryInitializer` for automatically collected incoming `RequestTelemetry` can run before `CorrelationIdMiddleware`. At that point, `HttpContext.TraceIdentifier` still contains the ASP.NET Core request identifier and Activity baggage has not been set. Changing only the initializer source priority therefore does not solve incoming request telemetry enrichment.

The implemented approach separates responsibilities:

- a new API-specific `ApplicationInsightsCorrelationIdMiddleware` runs immediately after `UseCorrelationId()` and directly enriches the existing `HttpContext.Features.Get<RequestTelemetry>()` object;
- request telemetry receives only the final validated GUID from `HttpContext.TraceIdentifier`;
- an existing `CorrelationId` property is not overwritten;
- `CorrelationIdTelemetryInitializer` remains the fallback for dependency, trace, exception, and telemetry without an active request feature;
- W3C `operation_Id` is not changed;
- the Framework correlation middleware remains independent of Application Insights packages.

DEV App Service autoinstrumentation was operationally disabled before this source patch, while `APPLICATIONINSIGHTS_CONNECTION_STRING` remains available to the code-based SDK. A local lifecycle test through the real API host confirms that the received or generated GUID is present in both `RequestTelemetry.Properties["CorrelationId"]` and the response header.

## Summary

`Service.Api.Template` currently has automatically collected incoming request and outgoing HTTP dependency records in Azure. The end-to-end view already shows the HTTP method, path, status, success/failure, duration, and Application Insights `operation_Id`. However, application `ILogger<T>` events are not visible in `traces`, and the business `CorrelationId` is not present in the custom properties of request, dependency, or trace telemetry.

The goal is to add code-controlled Application Insights integration that uses the existing `APPLICATIONINSIGHTS_CONNECTION_STRING` from Azure configuration, preserves the current Serilog file sink, and provides safe tracking of the same `CorrelationId` across the incoming request, application logs, and outgoing CRM calls.

Application Insights does not model a request and response as two separate records. A single `request` or `dependency` record represents the complete request-response cycle and includes status, result, and duration. Full request/response bodies must not be logged automatically.

## Agreed Scope

- Add standard Application Insights telemetry registration to `Service.Api.Template`.
- Read the connection string exclusively from `APPLICATIONINSIGHTS_CONNECTION_STRING`.
- Do not hardcode DEV, UAT, PROD, slot names, or connection string values.
- Preserve the current Serilog setup and file sink.
- Forward existing `ILogger<T>` events to Application Insights `traces`.
- Preserve `CorrelationId` as a custom property of application logs.
- Add the same `CorrelationId` as a custom property of incoming request and outgoing dependency telemetry, including CRM calls.
- Use only the canonical `Correlation-ID` HTTP header; do not introduce compatibility with an alternative or parallel name.
- Preserve automatic W3C/Application Insights correlation through `operation_Id`; the business `CorrelationId` is an additional identifier, not a replacement for `operation_Id`.
- Do not log Authorization/Bearer tokens, client secrets, passwords, complete headers, or unmasked sensitive data.
- Do not introduce automatic logging of full request and response bodies.
- Do not change business logic, controllers, DTO models, or CRM contracts unless technically required for telemetry.

## Confluence Requirements

The `Logging, Correlation & Observability` section requires capturing:

- Correlation ID;
- Request ID;
- source client/system;
- endpoint/operation;
- relevant external identifiers;
- downstream target;
- result;
- processing duration;
- stable error code.

Access tokens, secrets, passwords, and unmasked sensitive data must not be logged. The expected reference flow is `Client -> Service API Template -> CRM API`, with the same correlation ID throughout the transaction.

Automatic Application Insights telemetry covers basic request/dependency data, but source system, business identifiers, and stable business error codes remain the responsibility of structured application logs.

## Current Behavior

### Logging

- `Template.Service.Api/Program.cs` registers Serilog through `builder.Host.UseSerilog(...)`.
- `Template.Service.Api/appsettings.json` configures the daily file sink and displays `{CorrelationId}`.
- `Framework.Logger/Correlation/CorrelationIdMiddleware.cs` places the final GUID in `HttpContext.TraceIdentifier`, the request/response header, and the logging scope.
- Existing `ILogger<T>` calls are visible in the file log, but the supplied Azure end-to-end transaction view shows `0 Traces`.

### Request and dependency telemetry

- Azure already displays the incoming `REQUEST` for `Service.Api.Template`.
- Azure already displays outgoing dependencies for Keycloak and CRM.
- A request/dependency record contains the response status and duration; separate response telemetry is unnecessary.
- Application Insights `operation_Id` correlates the automatic transaction.
- The custom property `CorrelationId` is currently not visible.

### Packages and configuration

- `Template.Service.Api/Template.Service.Api.csproj` targets `net10.0`.
- `Microsoft.ApplicationInsights.AspNetCore` is not currently referenced.
- Azure App Service and slots have separate Application Insights resources and `APPLICATIONINSIGHTS_CONNECTION_STRING` values.
- It has not been confirmed whether the App Service auto-instrumentation agent is active or only the resource/connection string is configured.

## Expected Behavior

### Incoming Service.Api.Template call

Every incoming call should have request telemetry containing:

- HTTP method and route;
- status code and success/failure result;
- total duration;
- Application Insights `operation_Id`;
- custom property `CorrelationId`.

### Outgoing CRM and other HttpClient calls

Every outgoing call should have dependency telemetry containing:

- downstream target;
- HTTP method and safe path;
- status code and success/failure result;
- duration;
- the same `operation_Id` within the distributed transaction;
- the same business `CorrelationId` as the incoming request.

### Application logs

Existing calls:

```csharp
_logger.LogInformation(...);
_logger.LogWarning(...);
_logger.LogError(...);
```

must continue to be written to the existing Serilog file sink and become available in Application Insights `traces`. `CorrelationId` must be available as a structured/custom property, not only as part of the formatted message.

## Proposed Technical Approach

1. Add a compatible stable `Microsoft.ApplicationInsights.AspNetCore` package to the API project. As of the analysis date, the current stable version is `3.1.2` and is compatible with `net10.0`; confirm the version immediately before implementation.
2. Register `builder.Services.AddApplicationInsightsTelemetry()` without passing a connection string in code.
3. Preserve the existing `builder.Host.UseSerilog(...)`, with the minimum configuration needed to forward Serilog events to other registered `ILoggerProvider` implementations (`writeToProviders: true`) if targeted testing confirms the expected behavior.
4. Enable OpenTelemetry logging scopes (`IncludeScopes = true`) so the existing `CorrelationId` scope is available in Application Insights logs.
5. Add a small OpenTelemetry enrichment/processor or equivalent centralized solution that adds the final `CorrelationId` to incoming request and child dependency activities.
6. Do not add `Serilog.Sinks.ApplicationInsights` if the provider bridge satisfies the requirement; this avoids an unnecessary package and possible duplicate logs.
7. Do not create manual request/dependency records that duplicate automatic telemetry.
8. During refactoring, reuse `CorrelationIdConstants.HeaderName` for the `Correlation-ID` HTTP header; keep the `CorrelationId` telemetry property separate through `CorrelationIdConstants.LoggingPropertyName` or an equally centralized solution.

The exact processor implementation must be confirmed by targeted tests for ASP.NET Core and `HttpClient` activities. The solution must remain centralized and must not require changes to every service method.

## Azure / Deployment Boundary

Functional implementation belongs in code. Azure configuration remains the source of the destination connection string.

Verify:

- every DEV/UAT/PROD App Service or slot has the correct `APPLICATIONINSIGHTS_CONNECTION_STRING`;
- slot-specific values are marked as deployment slot settings when required;
- whether `ApplicationInsightsAgent_EXTENSION_VERSION`, `APPLICATIONINSIGHTS_ENABLE_AGENT`, or related auto-instrumentation settings are present;
- that two parallel instrumentation mechanisms are not active;
- that deployment does not produce duplicate request, dependency, or trace records.

If the Azure auto-instrumentation agent is active, coordinate the decision for the code SDK to become the authoritative mechanism and disable the agent if required. Existing Application Insights resources and connection strings remain valid.

## Affected Components

| Component | File/Class | Expected impact |
|---|---|---|
| API package | `Template.Service.Api/Template.Service.Api.csproj` | Application Insights SDK reference |
| Startup | `Template.Service.Api/Program.cs` | Telemetry registration, Serilog provider bridge, and scope configuration |
| Correlation infrastructure | `Framework.Logger/Correlation/CorrelationIdConstants.cs`, `CorrelationIdMiddleware.cs`, or a new centralized telemetry component | Preserve the canonical `Correlation-ID` header and enrich request/dependency activities with the business `CorrelationId` property |
| Logging configuration | `Template.Service.Api/appsettings.json` | Preserve the file sink; change only if a provider log-level filter is required, without a connection string |
| Tests | `Framework.Logger.Tests` and/or `Template.Service.Api.Tests` | Verify scopes/enrichment and absence of duplication |

## Acceptance Criteria

- [x] The API builds with the Application Insights SDK on `net10.0`.
- [x] The application reads its destination exclusively from `APPLICATIONINSIGHTS_CONNECTION_STRING`.
- [x] The existing Serilog file sink and configuration remain active.
- [ ] `LogInformation`, `LogWarning`, and `LogError` events appear in Application Insights `traces` according to configured levels and sampling rules.
- [ ] `CorrelationId` is a structured property of relevant trace records.
- [ ] Incoming request telemetry contains `CorrelationId` as a custom property.
- [ ] Outgoing CRM dependency telemetry contains the same `CorrelationId` as the incoming request.
- [x] All incoming, response, and outgoing HTTP headers use the canonical `Correlation-ID` name through the central constant.
- [x] The refactoring does not introduce an alternative alias or a new correlation header string literal.
- [ ] Incoming request and outgoing dependencies share the expected Application Insights `operation_Id` flow.
- [ ] Request/dependency telemetry shows method, path/target, status, result, and duration.
- [ ] No duplicate request/dependency/trace records are created by parallel instrumentation.
- [x] Authorization headers, tokens, secrets, passwords, and complete sensitive payloads were not introduced into logging.
- [x] No environment-specific values were added to source code or `appsettings*.json` files.
- [x] Business logic, controllers, DTO models, and CRM contracts remain unchanged.

## Validation Plan

- [x] `dotnet build Template.Service.Api.sln --no-restore --configuration Debug` - 0 errors, 9 existing warnings.
- [x] Run targeted and complete correlation and API startup/DI tests - 13/13 and 128/128 passed.
- [ ] Locally or in an isolated test environment, verify that the file sink and Application Insights provider receive the same `ILogger<T>` event without duplication.
- [ ] Send a request with a known `Correlation-ID` header and confirm the same value in `traces`, incoming request, and CRM dependency custom properties.
- [ ] Send a request without a `Correlation-ID` header and confirm that the generated value is identical across all three telemetry types.
- [ ] Verify that the response and all outgoing service calls use only the `Correlation-ID` header.
- [ ] Verify request status/duration and CRM dependency status/duration.
- [ ] Inspect Azure `End-to-end transaction details`: one request, expected dependencies, and related traces.
- [ ] Verify that no Authorization/Bearer tokens or request/response bodies are present in telemetry.
- [ ] Repeat smoke validation against DEV, UAT, and PROD resources without comparing or exposing connection string values.

## Risks and Open Questions

| # | Item | Proposal / next step |
|---|---|---|
| 1 | It is not confirmed whether Azure App Service uses the auto-instrumentation agent. | Inspect App Service environment settings before deployment and select one authoritative instrumentation mechanism. |
| 2 | The Serilog provider bridge with AI 3.x and Autofac creates a circular DI dependency. | Resolved with the compatible AI 2.23 SDK and standard Serilog sink sharing one `TelemetryConfiguration`. |
| 3 | A logging scope alone does not guarantee request/dependency custom properties. | Resolved with centralized Activity baggage plus `ITelemetryInitializer` enrichment and targeted tests. |
| 4 | Application Insights uses its own W3C `operation_Id`, separate from the business `CorrelationId`. | Preserve both identifiers and document their roles. |
| 5 | Application Insights sampling can affect the visibility of individual records. | Start with default behavior, then assess volume, cost, and the required sampling policy. |
| 6 | Logging full payloads would increase security and cost risks. | Restrict telemetry to metadata and explicitly approved safe business identifiers. |
| 7 | The header name in the external specification was not aligned with the local project. | Closed on 2026-09-01: the canonical name is `Correlation-ID`; external Atlassian content is not changed as part of this work. |

## Out of Scope for the Implementation
- Changes to Azure resources or connection string values.
- Logging complete request/response bodies.
- Introducing dashboards, alert rules, or a final sampling policy.
- Introducing another correlation header alias; the canonical name has already been agreed as `Correlation-ID`.



