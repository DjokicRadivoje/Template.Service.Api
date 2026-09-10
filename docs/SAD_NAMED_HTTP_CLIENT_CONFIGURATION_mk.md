# Named HttpClient конфигурација

## Summary

`ProductService` повеќе не користи hardcoded целосен URL. Product API base address доаѓа од конфигурацијата, а HTTP повикот користи named клиент `ProductApi`.

## Implemented Changes

- Додаден е `ApiPaths:ProductApiBaseAddress` во `appsettings.Development.json`.
- Додадено е централното име `HttpClientNames.ProductApi`.
- Named клиентот е регистриран со постоечкиот correlation handler.
- `ProductService` користи `IHttpClientFactory` и релативна патека `users`.
- Додадени се тестови за изборот на named клиентот, формирањето на request URI и Autofac resolution.

## Affected Components

- `Template.Service.Api` - конфигурација и регистрација на клиентот.
- `Template.Service.Services.Interfaces` - централно име на клиентот.
- `Template.Service.Services` - користење на named клиентот.
- `Template.Service.Api.Tests` - насочен тест.

## Configuration / Deployment Impact

Секоја околина мора да обезбеди `ApiPaths:ProductApiBaseAddress`. Development вредноста е вклучена во `appsettings.Development.json`.

## Validation

- Solution build: успешен, 0 грешки и 2 претходно постоечки nullable предупредувања.
- Тестови: 23/23 успешни.

## Further Improvements

- За нови downstream API-ја да се додаваат посебни named клиенти, адреси и resilience политики.

## Change Log - 2026-08-19

- Додаден е named клиент `HRApi` покрај постоечкиот `ProductApi`.
- Регистрацијата на поединечните клиенти е преместена од `Program.cs` во `Template.Service.DependencyInjection` composition root.
- `Program.cs` користи еден делегирачки повик за регистрација на сите сервисни HTTP клиенти.
- `ProductService` ги креира двата потребни клиенти во конструкторот кога DI го разрешува сервисот; клиентите не се креираат во поединечните методи.
- Двата клиенти го користат correlation handler-от.
- Build е успешен и тестовите поминуваат (26/26).

