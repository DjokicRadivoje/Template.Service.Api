# Service.Api.Template - Implementation Log

## Context

- Task: `API_TEMPLATE_RENAMING`
- Branch: nije primenljivo; folder još nije Git repozitorijum
- Started: 2026-09-09
- Analysis: `docs/API_TEMPLATE_RENAMING_ANALIZA_sr.md`

## Timeline

- 2026-09-09 - potvrđen naziv `Service.Api.Template` i izvorni token `Template.Service`.
- 2026-09-09 - preimenovani solution, projekti, namespace-i, `using` direktive, project reference-i, konfiguracioni identitet i testovi.
- 2026-09-09 - typo `Service.Dependecy` ispravljen je kroz novi projekat `Template.Service.DependencyInjection`.
- 2026-09-09 - zadržani `Framework.Logger` projekti bez promene naziva.
- 2026-09-09 - infrastrukturna dokumentacija mehanički je usklađena sa novim nazivima i putanjama; deset naziva dokumenata je preimenovano.
- 2026-09-09 - restore, build i kompletni testovi prošli nad `Service.Api.Template.sln`.

## Implementation Notes

- `Service.Api.Template.sln` - novi solution naziv i projektne putanje.
- `Template.Service.Api` - glavni API projekat, launch profil, application name i user-secrets ID.
- `Template.Service.Api.Tests` - test projekat i namespace-i.
- `Template.Service.BusinessLogic` / `Template.Service.BusinessLogic.Interfaces` - BL slojevi.
- `Template.Service.BusinessModel` / `Template.Service.DataModel` - model slojevi.
- `Template.Service.Services` / `Template.Service.Services.Interfaces` - servisni slojevi.
- `Template.Service.Mapper` - mapping sloj.
- `Template.Service.DependencyInjection` - composition root.
- `Framework.Logger` / `Framework.Logger.Tests` - namerno zadržani generički infrastrukturni nazivi.
- `CrmController` i `OrganizationsController` - nazivi i rute nisu menjani.

## Problems / Bugs

- Date: 2026-09-09
- Symptom: Visual Studio je držao root foldere tri projekta i blokirao direktni directory rename.
- Cause: otvoren solution/design-time build proces.
- Fix: source stavke tih projekata bezbedno su premeštene pojedinačno u nove foldere; stari folderi sadrže samo ignorisane `bin`/`obj` artefakte.
- Status: fixed and verified; nakon zatvaranja solution-a uklonjeni su svi stari folderi i generisani artefakti

## Validation Log

- `dotnet restore Service.Api.Template.sln -p:Configuration=Debug` - uspešno.
- `dotnet build Service.Api.Template.sln --no-restore --configuration Debug` - uspešno, 0 grešaka; 5 poznatih `NU1903` upozorenja za AutoMapper 12.0.1.
- `dotnet test Service.Api.Template.sln --no-build --no-restore --configuration Debug` - uspešno; 74/74 API i 13/13 Framework.Logger testova.
- Aktivni source pregled - nema starih project/namespace identiteta.
- Stari `Web.Service.*` folderi - uklonjeni nakon zatvaranja solution-a u Visual Studio-u.
- Generisani `.vs`, `bin` i `obj` artefakti - uklonjeni; završna provera pokazuje 0 preostalih direktorijuma.
- JSON konfiguracije - sve četiri `appsettings` varijante uspešno parsirane.
- Završni high-confidence secret scan - nema prepoznatih private key, cloud key, token ili connection-string obrazaca.

## Follow-up

- Po želji preimenovati spoljašnji workspace folder u `Service.Api.Template` kada više nije aktivan workspace.
- Definisati nove nazive i rute za CRM/Organizations referentne primere.
- Kreirati stvarni .NET template manifest sa `sourceName: Template.Service` i short name `service-api`.
- AutoMapper upgrade voditi kao poseban dependency task.
