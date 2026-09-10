# Migracija Template.Service.Api solution na .NET 10 - analiza

## Status

- Faza: implementacija završena i lokalno validirana; commit i CI validacija čekaju.
- Datum analize: 20. avgust 2026.
- Grana tokom analize: `main`.
- Zavisnost: ovaj deo je preduslov za serversku i klijentsku JWT implementaciju.
- Referentni dokument: `E:\GIT_2026\rafajlovski-chatbot_2026\keycloakJWT.md`.

## Cilj

Migrirati svih 12 projekata iz `Template.Service.Api.sln` sa `net8.0` na `net10.0`, uskladiti framework-zavisne pakete i potvrditi da postojeće ponašanje ostaje nepromenjeno pre uvođenja JWT i CRM funkcionalnosti.

Migracija je zasebna tehnička celina. U ovom delu se ne dodaju autentifikacija, autorizacija, Keycloak konfiguracija, CRM klijent ili nove poslovne metode.

## Trenutno stanje

- Svih 12 `.csproj` fajlova eksplicitno cilja `net8.0`.
- Na razvojnoj mašini je instaliran .NET SDK `10.0.400`.
- Ne postoji root `global.json`, pa izbor SDK-a zavisi od okruženja u kojem se komanda izvršava.
- Eksplicitne `Microsoft.Extensions.*` reference su na 8.x verzijama.
- `Serilog.AspNetCore` je na 8.x verziji.
- Solution koristi Autofac, Serilog, Swashbuckle, AutoMapper i xUnit pakete koje treba proveriti na .NET 10.
- Radno stablo je čisto u pogledu praćenih fajlova; postoje dva ranije zatečena untracked GitHub Actions YML fajla koji nisu deo ove migracije.

## Očekivano stanje

- Svi projekti ciljaju `net10.0`.
- Root `global.json` bira odobreni .NET 10 SDK i dozvoljava samo dogovoreni patch roll-forward.
- Framework-zavisne Microsoft reference su usklađene sa 10.x.
- Third-party paketi ostaju na postojećoj verziji ako je kompatibilna; menjaju se samo kada je to potrebno za .NET 10 kompatibilnost, bez nepotrebnog širenja scope-a.
- Solution se restore-uje, build-uje i testira na .NET 10 bez funkcionalnih izmena.
- CI workflow koristi .NET 10 SDK pre spajanja migracije.

## Obuhvaćeni projekti

1. `Framework.Logger`
2. `Framework.Logger.Tests`
3. `Template.Service.DependencyInjection`
4. `Template.Service.Api`
5. `Template.Service.Api.Tests`
6. `Template.Service.BusinessLogic`
7. `Template.Service.BusinessModel`
8. `Template.Service.DataModel`
9. `Template.Service.BusinessLogic.Interfaces`
10. `Template.Service.Services.Interfaces`
11. `Template.Service.Mapper`
12. `Template.Service.Services`

## Predloženo rešenje

### SDK pinning

Dodati `global.json` u root repozitorijuma. Početna vrednost prati instalirani SDK:

```json
{
  "sdk": {
    "version": "10.0.400",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  }
}
```

Pre commit-a potvrditi da CI i razvojna okruženja mogu da instaliraju ili koriste ovu SDK liniju. Ako organizacioni standard zahteva drugu .NET 10 feature band verziju, `global.json` treba uskladiti sa tim standardom.

### Target framework

U svih 12 projekata promeniti:

```xml
<TargetFramework>net8.0</TargetFramework>
```

u:

```xml
<TargetFramework>net10.0</TargetFramework>
```

Ne uvoditi multi-targeting osim ako se pojavi konkretan potrošač koji i dalje zahteva .NET 8.

### Paketi

- Uskladiti eksplicitne `Microsoft.Extensions.Configuration.Abstractions`, `Microsoft.Extensions.Http` i `Microsoft.Extensions.Logging.Abstractions` reference sa 10.x.
- Uskladiti `Serilog.AspNetCore` sa ASP.NET Core 10 kompatibilnom verzijom.
- Proveriti, ali ne menjati automatski, Autofac, Autofac.Extensions.DependencyInjection, Swashbuckle, AutoMapper i test pakete.
- Ne dodavati `Microsoft.AspNetCore.Authentication.JwtBearer` u ovoj fazi; on pripada serverskoj JWT analizi.
- Ne uvoditi central package management u okviru ove migracije, jer bi to bio zaseban refaktor.

## Tok implementacije

```text
Baseline build/test na net8.0
  -> dodavanje global.json
  -> promena svih TargetFramework vrednosti
  -> usklađivanje Microsoft/ASP.NET paketa
  -> restore
  -> build cele solution
  -> svi testovi
  -> pregled warning-a i runtime startup provera
  -> tek zatim JWT rad
```

## Pogođeni fajlovi

- `global.json` - novi SDK pinning dokument.
- svih 12 `.csproj` fajlova - target framework i potrebna usklađivanja paketa.
- `.github/workflows/*.yml` - samo ako postojeći workflow eksplicitno instalira stariji SDK; ova dva trenutno untracked fajla ne menjati bez zasebne provere njihovog vlasništva i namene.
- dokumentacija za migraciju i kasniji implementation log.

## Rizici

- Third-party paket može biti restore-kompatibilan, ali imati runtime problem na .NET 10.
- ASP.NET Core ili test host promena može otkriti ranije skrivene warning-e ili ponašanje.
- Pinovanje feature band-a koji nije dostupan u CI-u može blokirati build.
- Istovremena JWT izmena bi otežala razlikovanje migracionog problema od funkcionalnog problema.
- Neusaglašeni target framework-i mogu napraviti nepotrebne compatibility asset izbore.

## Otvorena pitanja

- Koju tačnu .NET 10 SDK feature band verziju koristi CI: `10.0.4xx` ili organizaciono odobrenu drugu verziju?
- Da li se postojeći GitHub Actions fajlovi usvajaju u okviru ove migracije ili ostaju van scope-a?
- Da li organizacija zahteva poseban container/base image update?

Ova pitanja ne blokiraju lokalni početak migracije, ali moraju biti rešena pre CI/deployment validacije.

## Plan validacije

1. Pre izmene zabeležiti rezultat postojećeg build-a i testova.
2. Pokrenuti `dotnet --version` i potvrditi SDK iz `global.json`.
3. Pokrenuti `dotnet restore Template.Service.Api.sln`.
4. Pokrenuti `dotnet build Template.Service.Api.sln --no-restore`.
5. Pokrenuti `dotnet test Template.Service.Api.sln --no-build` ili oba test projekta zasebno.
6. Pokrenuti `Template.Service.Api` sa postojećom development konfiguracijom i potvrditi startup.
7. Uporediti broj i tip warning-a sa baseline-om.
8. Proveriti da nema nenamernih funkcionalnih ili konfiguracionih izmena.

## Acceptance kriterijumi

- [x] Svih 12 projekata cilja `net10.0`.
- [x] `global.json` bira lokalno odobreni .NET 10 SDK `10.0.400`.
- [x] Restore prolazi.
- [x] Solution build prolazi bez grešaka.
- [x] Svi postojeći testovi prolaze, 26/26.
- [x] API se pokreće u development režimu; Swagger startup probe vraća HTTP 200.
- [x] Nema JWT/CRM funkcionalnih izmena u migracionom delu.
- [ ] CI je spreman da koristi .NET 10.

## Rezultat implementacije - 20. avgust 2026.

- Baseline `net8.0` build prolazi sa 4 postojeća nullable warning-a.
- Baseline testovi prolaze 26/26.
- Dodat je root `global.json` sa SDK `10.0.400` i `latestPatch` pravilom.
- Svih 12 projekata migrirano je na `net10.0`.
- Eksplicitne `Microsoft.Extensions.*` reference usklađene su na 10.0.11.
- `Serilog.AspNetCore` je usklađen na 10.0.0, `Serilog` na 4.3.0 i `Serilog.Sinks.File` na 7.0.0.
- .NET 10 restore i build prolaze; build ima 4 postojeća nullable warning-a i 5 propagiranih NU1903 upozorenja za ranjivi AutoMapper 12.0.1.
- .NET 10 testovi prolaze 26/26.
- Development startup provera prolazi sa HTTP 200 na Swagger stranici.
- Build bez eksplicitnog `-c Debug` pada zbog spoljne/prepostojeće MSBuild vrednosti `configuration-service/|Any CPU`; validacija je zato vođena eksplicitno sa `-c Debug`.
- AutoMapper major migracija nije uključena jer deprecated DI paket i licencne/major API posledice zahtevaju zasebnu odluku. Ranjivost ostaje otvoren high-severity follow-up.
- CI workflow nije menjan jer su dva `.github/workflows` fajla zatečena kao korisnički untracked fajlovi i nisu deo odobrenog scope-a.

## Preporučeni commit scope

```text
chore: migrate Template.Service.Api solution to .NET 10
```

Commit se pravi samo na eksplicitan zahtev korisnika.

