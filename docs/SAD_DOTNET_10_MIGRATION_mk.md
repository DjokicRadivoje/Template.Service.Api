# Миграција на Template.Service.Api solution на .NET 10

## Summary

Сите 12 проекти од `Template.Service.Api.sln` се мигрирани од .NET 8 на .NET 10 без промена на деловното однесување.

## Implemented Changes

- Додаден е `global.json` за SDK `10.0.400` со правило `latestPatch`.
- Сите 12 проекти сега таргетираат `net10.0`.
- Експлицитните Microsoft.Extensions пакети се усогласени на 10.0.11.
- Serilog ASP.NET Core пакетот и неговите директни зависности се усогласени со .NET 10 линијата.

## Affected Components

- `global.json` - избор на SDK.
- сите `.csproj` датотеки - target framework.
- `Template.Service.DependencyInjection`, `Template.Service.BusinessLogic`, `Template.Service.Services` - Microsoft.Extensions пакети.
- `Template.Service.Api` - Serilog пакети.

## Configuration / Deployment Impact

CI, runtime и container околините мора да имаат компатибилен .NET 10 SDK/runtime. Апликациските configuration клучеви не се променети.

## Validation

- Baseline .NET 8 build: passed, 4 постојни warning-и.
- Baseline тестови: 26/26 passed.
- .NET 10 restore/build: passed, 0 грешки.
- .NET 10 тестови: 26/26 passed.
- Development Swagger startup probe: HTTP 200.
- Отворено: AutoMapper 12.0.1 high-severity `NU1903`; потребен е посебен security follow-up.

## Further Improvements

- Да се валидира CI/container image на .NET 10.
- Да се реши AutoMapper ранливоста во посебна задача.
- Да се испита надворешната MSBuild `Configuration` вредност поради која е потребно експлицитно `-c Debug`.

