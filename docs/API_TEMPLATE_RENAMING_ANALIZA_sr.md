# Service.Api.Template - Analiza preimenovanja

## Context

Očišćeni Business API projekat treba preimenovati u generički `Service.Api.Template`. Interni izvorni token za projekte i namespace-e biće `Template.Service`, kako bi kasnije mogao da se zameni konkretnim imenom pri generisanju novog servisa.

## Current State

- Solution: `Web.Business.Api.sln`.
- Glavni projekat i namespace: `Web.Business.Api` / `Web.Service.Api`.
- Slojevi koriste prefiks `Web.Service`.
- DI projekat ima naziv sa typo-om `Service.Dependecy`.
- Test projekat koristi naziv `Web.Service.Api.Tests`.
- `Framework.Logger` je generička infrastrukturna komponenta bez Business identiteta.
- Workspace root folder je izvan Git-a i nosi stari naziv, ali njegovo pomeranje nije deo bezbednog in-workspace preimenovanja.

## Target State

- Solution/repository identitet: `Service.Api.Template`.
- Namespace i project token: `Template.Service`.
- Glavni projekat: `Template.Service.Api`.
- Ostali slojevi: `Template.Service.BusinessLogic`, `Template.Service.BusinessLogic.Interfaces`, `Template.Service.BusinessModel`, `Template.Service.DataModel`, `Template.Service.Mapper`, `Template.Service.Services`, `Template.Service.Services.Interfaces`.
- DI projekat: `Template.Service.DependencyInjection`.
- Test projekat: `Template.Service.Api.Tests`.
- `Framework.Logger` i `Framework.Logger.Tests` ostaju nepromenjeni jer predstavljaju generičku infrastrukturnu komponentu.
- `CrmController` i `OrganizationsController` ostaju pod postojećim nazivima i rutama u ovom tasku.

## Affected Components

- solution i svi project reference-i;
- folderi i `.csproj` fajlovi;
- namespace-i, `using` direktive i potpuno kvalifikovani tipovi;
- `Program.cs`, launch profile, Serilog application name i user-secrets ID;
- test namespace-i, test configuration i project reference-i;
- infrastrukturna dokumentacija sa starim nazivima i putanjama.

## Risks

- Visual Studio je otvoren i može ponovo kreirati stare `bin`/`obj` foldere kroz design-time build.
- Redosled zamena mora prvo obraditi specifične interfejs/test namespace-e, a zatim opšti `Web.Service` prefiks.
- Project reference-i moraju odgovarati novim fizičkim folderima i `.csproj` imenima.
- Mehaničko preimenovanje dokumentacije ne garantuje da svaki istorijski opis predstavlja trenutno template stanje; sadržaj se zadržava kao infrastrukturna istorija.
- Root workspace folder ne može se bezbedno preimenovati iz trenutnog workspace-a.

## Implementation Plan

1. Mehanički zameniti stare project/namespace identitete u source, test, configuration, solution i documentation fajlovima.
2. Preimenovati `.csproj` i solution fajl.
3. Preimenovati projektne foldere.
4. Proveriti da nema starih namespace-a ili project reference-a u aktivnom source-u.
5. Pokrenuti restore/build/test nad novim solution imenom.
6. Očistiti generisane `bin`/`obj` artefakte nakon validacije.
7. Ažurirati implementation log i SAD.

## Validation Plan

- `dotnet restore Service.Api.Template.sln --configuration Debug`;
- `dotnet build Service.Api.Template.sln --no-restore --configuration Debug`;
- `dotnet test Service.Api.Template.sln --no-build --no-restore --configuration Debug`;
- pretraga starih identiteta van istorijskog konteksta;
- provera solution/project reference putanja;
- potvrda da JSON konfiguracije ostaju validne.

