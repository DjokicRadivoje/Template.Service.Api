# Correlation ID

## Summary

The API now uses one `Correlation-ID` across the incoming request, processing pipeline, outgoing HTTP calls, logs, and HTTP response.

## Implemented Changes

- Only GUID values in `D` format are accepted; an invalid value is replaced with a new ID and a warning is logged.
- The middleware sets the final ID in `HttpContext.TraceIdentifier` and the request and response headers.
- A `DelegatingHandler` automatically propagates the same ID through the default `HttpClient` pipeline.
- The Serilog file sink displays the structured `CorrelationId` property.
- The previous `X-UniqueTrace-Id` mechanism was removed.

## Affected Components

- `Framework.Logger/Correlation` - middleware, handler, constants, and DI extensions.
- `Template.Service.Api/Program.cs` - correlation pipeline registration.
- `Template.Service.Api/appsettings.json` - log output format.
- `Framework.Logger.Tests` - 12 automated tests.

## Configuration / Deployment Impact

No new configuration keys or migrations are required. The text Serilog output template changed. Clients should use the `Correlation-ID` header.

## Validation

- Solution Debug build: passed with 0 errors.
- Tests: 12/12 passed.
- Runtime response and Serilog correlation verification: passed.

## Further Improvements

- Future background jobs with multiple downstream calls should establish one shared correlation scope.

