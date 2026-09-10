# Migracija Template.Service.Api solution na .NET 10

## Summary

Svih 12 projekata iz `Template.Service.Api.sln` migrirano je sa .NET 8 na .NET 10 bez promene poslovnog ponašanja.

## Implemented Changes

- Dodat je `global.json` za SDK `10.0.400` sa `latestPatch` pravilom.
- Svih 12 projekata sada cilja `net10.0`.
- Eksplicitni Microsoft.Extensions paketi usklađeni su na 10.0.11.
- Serilog ASP.NET Core paket i njegove direktne zavisnosti usklađeni su sa .NET 10 linijom.

## Affected Components

- `global.json` - izbor SDK-a.
- svi `.csproj` fajlovi - target framework.
- `Template.Service.DependencyInjection`, `Template.Service.BusinessLogic`, `Template.Service.Services` - Microsoft.Extensions paketi.
- `Template.Service.Api` - Serilog paketi.

## Configuration / Deployment Impact

CI, runtime i container okruženja moraju imati kompatibilan .NET 10 SDK/runtime. Aplikacioni configuration ključevi nisu menjani.

## Validation

- Baseline .NET 8 build: passed, 4 postojeća warning-a.
- Baseline testovi: 26/26 passed.
- .NET 10 restore/build: passed, 0 grešaka.
- .NET 10 testovi: 26/26 passed.
- Development Swagger startup probe: HTTP 200.
- Otvoreno: AutoMapper 12.0.1 high-severity `NU1903`; zahteva zaseban security follow-up.

## Further Improvements

- Validirati CI/container image na .NET 10.
- Rešiti AutoMapper ranjivost zasebnim zadatkom.
- Ispitati spoljnu MSBuild `Configuration` vrednost koja zahteva eksplicitni `-c Debug`.

