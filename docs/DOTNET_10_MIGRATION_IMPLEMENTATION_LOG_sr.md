# Migracija Template.Service.Api solution na .NET 10 - Implementation Log

## Context

- Tema: .NET 10 migracija
- Grana: `main`
- Datum: 20. avgust 2026.
- Analysis: `docs/DOTNET_10_MIGRATION_ANALIZA_sr.md`
- Status: implementirano i lokalno validirano; pending commit i CI.

## Timeline

- 2026-08-20 - Potvrđeno je da svih 12 projekata cilja `net8.0` i da je SDK `10.0.400` instaliran.
- 2026-08-20 - Baseline build bez `-c` pao je pre kompilacije zbog nevalidne spoljne MSBuild konfiguracije `configuration-service/|Any CPU`.
- 2026-08-20 - Baseline build sa `-c Debug` prošao je sa 4 postojeća nullable warning-a.
- 2026-08-20 - Baseline testovi prošli su 26/26.
- 2026-08-20 - Dodat je `global.json`, svih 12 projekata migrirano je na `net10.0`, a framework-zavisni paketi su usklađeni.
- 2026-08-20 - Prvi restore u sandbox-u pao je zbog blokiranog mrežnog pristupa; ponovljeni odobreni NuGet restore prošao je.
- 2026-08-20 - Restore je prijavio high-severity `NU1903` za postojeći AutoMapper 12.0.1.
- 2026-08-20 - .NET 10 build prošao je sa 0 grešaka i 9 warning-a.
- 2026-08-20 - .NET 10 testovi prošli su 26/26.
- 2026-08-20 - Development startup/Swagger probe vratio je HTTP 200.

## Implementation Notes

- `global.json` - pinovan SDK `10.0.400`, `latestPatch`, bez prerelease verzija.
- svih 12 `.csproj` fajlova - `net8.0` promenjen u `net10.0`.
- `Template.Service.DependencyInjection.csproj` - Microsoft Configuration/Http reference usklađene na 10.0.11.
- `Template.Service.BusinessLogic.csproj` - Logging.Abstractions usklađen na 10.0.11.
- `Template.Service.Services.csproj` - Http i Logging.Abstractions usklađeni na 10.0.11.
- `Template.Service.Api.csproj` - Serilog ASP.NET Core linija usklađena sa .NET 10.
- Nisu uvedene JWT, CRM ili poslovne izmene.

## Problems / Bugs

### Spoljna MSBuild konfiguracija

- Symptom: `dotnet build Template.Service.Api.sln` koristi `configuration-service/|Any CPU` i pada sa MSB4126.
- Cause: spoljna/prepostojeća `Configuration` vrednost, nije uvedena migracijom.
- Workaround: eksplicitno `-c Debug`.
- Status: otvoren environment follow-up; ne utiče na eksplicitni Debug build.

### AutoMapper high-severity upozorenje

- Symptom: pet projekata prijavljuje `NU1903`, `GHSA-rvv3-g6hj-g44x`.
- Cause: postojeći deprecated `AutoMapper.Extensions.Microsoft.DependencyInjection` 12.0.1 uvodi AutoMapper 12.0.1.
- Decision: ne raditi major/licensing migraciju u čistom .NET 10 scope-u.
- Status: otvoren high-severity follow-up; potrebno je zasebno uklanjanje AutoMapper-a ili migracija na patched verziju.

## Validation Log

- `dotnet build Template.Service.Api.sln -c Debug` na net8 baseline-u - passed, 4 warning-a, 0 grešaka.
- `dotnet test Template.Service.Api.sln -c Debug --no-build` na net8 baseline-u - passed, 26/26.
- `dotnet restore Template.Service.Api.sln -p:Configuration=Debug` - passed nakon odobrenog mrežnog pristupa.
- `dotnet build Template.Service.Api.sln -c Debug --no-restore` na net10 - passed, 9 warning-a, 0 grešaka.
- `dotnet test Template.Service.Api.sln -c Debug --no-build` na net10 - passed, 26/26.
- Development startup probe na `http://127.0.0.1:5059/swagger/index.html` - HTTP 200; proces zatvoren nakon provere.

## Commits

- Pending commit.

## Deployment / Environment

- Dev: lokalno validirano na SDK `10.0.400`.
- CI: nije validirano; postojeći untracked workflow fajlovi nisu menjani.
- Prod: nije deploy-ovano.
- Config: nema aplikacionih konfiguracionih promena.

## Follow-up

- Rešiti AutoMapper high-severity ranjivost u zasebnom bezbednosnom zadatku.
- Potvrditi .NET 10 SDK feature band u CI-u i runtime/container image-u.
- Ispitati poreklo spoljne `Configuration=configuration-service/` vrednosti.

