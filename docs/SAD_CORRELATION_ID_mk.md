# Correlation ID

## Summary

API сега користи еден `Correlation-ID` низ влезното барање, обработката, излезните HTTP повици, логовите и HTTP одговорот.

## Implemented Changes

- Се прифаќа исклучиво GUID во `D` формат; невалидна вредност се заменува со нов ID и се запишува warning.
- Middleware го поставува финалниот ID во `HttpContext.TraceIdentifier`, request и response header.
- `DelegatingHandler` автоматски го проследува истиот ID преку default `HttpClient` pipeline.
- Serilog file sink го прикажува структурираното својство `CorrelationId`.
- Стариот `X-UniqueTrace-Id` механизам е отстранет.

## Affected Components

- `Framework.Logger/Correlation` - middleware, handler, константи и DI extensions.
- `Template.Service.Api/Program.cs` - регистрација на correlation pipeline.
- `Template.Service.Api/appsettings.json` - формат на log излезот.
- `Framework.Logger.Tests` - 12 автоматски тестови.

## Configuration / Deployment Impact

Нема нови конфигурациски клучеви или миграции. Променет е текстуалниот Serilog output template. Клиентите треба да го користат header-от `Correlation-ID`.

## Validation

- Solution Debug build: успешен, 0 грешки.
- Тестови: 12/12 успешни.
- Runtime response и Serilog correlation проверка: успешна.

## Further Improvements

- Идните background jobs со повеќе downstream повици треба да отворат заеднички correlation scope.

