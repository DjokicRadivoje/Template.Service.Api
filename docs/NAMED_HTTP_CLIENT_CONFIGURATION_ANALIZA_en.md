# Named HttpClient Configuration - Analysis and Agreement

## Context

`ProductService` used the default `HttpClient` registered with an empty name and a hardcoded full URL, `https://jsonplaceholder.typicode.com/users`. This approach coupled the service to a specific environment and made adding new downstream APIs more difficult.

## Agreed Solution

- Introduce an `ApiPaths` section in `appsettings.Development.json`, following the structure of the existing application example.
- For the initial scope, add only `ProductApiBaseAddress` with the value `https://jsonplaceholder.typicode.com/`.
- Register a named `HttpClient` under the name `ProductApi`.
- Centralize the client name through `HttpClientNames.ProductApi`.
- Inject `IHttpClientFactory` into `ProductService`, create the named client, and call the relative `users` path.
- Keep the existing correlation handler in the named client's pipeline so that it adds `Correlation-ID` to downstream calls.
- Validate the base address at application startup; it must be an absolute URI.

## Configuration and Call Flow

```text
appsettings.<Environment>.json
    ApiPaths:ProductApiBaseAddress
        -> Program.cs registers the ProductApi HttpClient
            -> ProductService creates the ProductApi client
                -> GET users
                    -> CorrelationIdHandler
```

## Configuration / Deployment Impact

The development value is defined in `appsettings.Development.json`. Every other environment must provide the same key through the corresponding environment-specific appsettings file, an environment variable, or another configuration provider.

## Validation

- The solution build passed with 0 errors and 2 pre-existing nullable warnings.
- All tests passed (23/23).
- A test was added to verify the `ProductApi` client name and construction of the final `.../api/users` URI from the base address and relative path.
- A test was added to verify Autofac resolution of `IProductService` with a registered `IHttpClientFactory`.

## Further Improvements

- Add a separate client name and `ApiPaths` key for each new downstream API.
- Introduce timeout, retry/circuit-breaker policies, and authentication per API when required by the business use case.

## Update - Multiple Clients and Composition Root (2026-08-19)

A second downstream API, `HRApi`, was introduced with the following agreement:

- `Program.cs` does not know individual client names or configuration keys.
- `Program.cs` delegates HTTP client registration to the `Template.Service.DependencyInjection` composition root through a single call.
- `Template.Service.DependencyInjection` registers configuration recipes for `ProductApi` and `HRApi`, their base addresses, and the shared correlation handler.
- Registering these recipes at startup does not create `HttpClient` instances or open network connections.
- `ProductService` creates both required named clients in its constructor, only when DI resolves the service.
- Deferred client creation inside individual service methods was not introduced.
- A network connection is established only when `GetAsync`/`SendAsync` is called.

```text
Program.cs
    -> DependencyInjectionConfig.ConfigureHttpClients(...)
        -> registers ProductApi and HRApi recipes

DI resolves ProductService
    -> CreateClient(ProductApi)
    -> CreateClient(HRApi)

Service method call
    -> sends an HTTP request through the appropriate client
```

Both configuration keys are validated as absolute URIs during registration. The build passed with 0 errors and 4 pre-existing nullable warnings in the Product/Employee DTO models, and all tests passed (26/26).

