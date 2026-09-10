# API Template Cleanup - Implementation Log

## Context

- Task: `API_TEMPLATE_CLEANUP`
- Branch: nije primenljivo; folder još nije Git repozitorijum
- Started: 2026-09-09
- Analysis: `docs/API_TEMPLATE_CLEANUP_ANALIZA_sr.md`

## Timeline

- 2026-09-09 - potvrđen scope: ostaju CRM i Organizations primeri i generička infrastruktura.
- 2026-09-09 - baseline solution build prošao sa 9 upozorenja i bez grešaka.
- 2026-09-09 - baseline testovi prošli: 212 API testova i 13 Framework.Logger testova.
- 2026-09-09 - uklonjeno osam ostalih kontrolera i njihove isključive BL, service, model, mapper, DI i test zavisnosti.
- 2026-09-09 - `ICrmService` i `CrmService` svedeni na `GetMockData` i `GetOrganizations`.
- 2026-09-09 - uklonjena outbound Keycloak client-credentials infrastruktura koju je koristio samo Accounting tok; inbound Keycloak/JWT infrastruktura je sačuvana.
- 2026-09-09 - environment konfiguracija svedena na inbound authentication i CRM primer, uz `example.invalid` URL-ove i javne placeholder vrednosti.
- 2026-09-09 - dodat korenski `.gitignore` za IDE/build artefakte, lokalnu konfiguraciju i tipične secret fajlove.
- 2026-09-09 - finalni source build i testovi prošli.

## Implementation Notes

- `Template.Service.Api/Controllers` - ostali su samo `CrmController` i `OrganizationsController`.
- `Template.Service.BusinessLogic` i `Template.Service.BusinessLogic.Interfaces` - ostala su dva referentna toka i zajednički response/error helper-i.
- `Template.Service.Services/CrmService.cs` - uklonjene metode svih obrisanih domena; dve zadržane metode nisu funkcionalno menjane.
- `Template.Service.Services.Interfaces/ICrmService.cs` - ugovor sveden na dve metode potrebne referentnim kontrolerima.
- `Template.Service.BusinessModel` i `Template.Service.DataModel` - uklonjeni Accounting, Customer, Product, Subscription, Training, Employee i nekorišćeni data paged modeli.
- `Template.Service.Mapper/DefaultProfile.cs` - ostale su samo dve CRM mape.
- `Template.Service.DependencyInjection/DependencyInjectionConfig.cs` - ostale su CRM/Organizations registracije, CRM named klijenti i CRM authentication handler/provider.
- `Template.Service.Services.Interfaces/Infrastructure/Http/HttpClientNames.cs` - ostali su samo `CRMApi` i `CRMToken`.
- `Template.Service.Api.Tests` - uklonjeni testovi obrisanih funkcija, a mešoviti test fajlovi svedeni su na CRM, Organizations i infrastrukturne scenarije.
- `Template.Service.Api/appsettings.*.json` - uklonjeni Product, HR, Accounting, subscription i outbound Keycloak ključevi; preostale vrednosti su sanitizovane.
- `.gitignore` - sprečava buduće dodavanje `.vs`, `bin`, `obj`, test output-a, logova i uobičajenih lokalnih secret fajlova.

## Problems / Bugs

- Date: 2026-09-09
- Symptom: prvi baseline build nije pokrenuo kompilaciju zbog nasleđene environment vrednosti `configuration-service/|Any CPU`.
- Cause: spoljašnja MSBuild konfiguracija nije postojala u solution fajlu.
- Fix: build je pokrenut sa eksplicitnim `--configuration Debug`.
- Status: verified

- Date: 2026-09-09
- Symptom: prvi test prolaz nakon cleanup-a imao je dva pada.
- Cause: fokusirani DI test nije registrovao AutoMapper, a integration test host nije dovoljno rano override-ovao sanitizovanu inbound authentication konfiguraciju.
- Fix: test setup sada registruje `DefaultProfile`, a test host eksplicitno postavlja authentication vrednosti kroz `UseSetting`.
- Status: fixed and verified

## Validation Log

- `dotnet build Template.Service.Api.sln --no-restore --configuration Debug` - uspešno; 0 grešaka, 5 `NU1903` upozorenja za AutoMapper 12.0.1.
- `dotnet test Template.Service.Api.sln --configuration Debug --no-restore` - uspešno; API 74/74 i Framework.Logger 13/13.
- Source pretraga uklonjenih domena - nema preostalih referenci van istorijske dokumentacije.
- Heuristički credential scan source/config/test/docs fajlova - nema prepoznatih stvarnih credential obrazaca; pronađena je jedna eksplicitna lažna `test-secret` vrednost u unit test fixture-u.
- URL pregled van `docs/` - nema URL-ova van localhost/public/example domena.
- Dokumentacioni URL pregled - dva Application Insights dokumenta sadržala su interni Atlassian link; linkovi su uklonjeni. Četiri ostala pogotka pripadaju javnom demo API-ju i nisu bezbednosno sporni.

## Follow-up

- Sadržajno proveravati istorijske infrastrukturne dokumente u odnosu na očišćeni template; nazivi i putanje su generalizovani, a interni linkovi uklonjeni.
- Odlučiti da li fizički ukloniti postojeće `.vs`, `bin` i `obj` direktorijume; novi `.gitignore` ih već isključuje iz budućeg repozitorijuma.
- Proveriti da li root status endpoint i health check treba da dele `/` rutu.
- Proceniti nekorišćeni `RoleClientId` property u inbound Keycloak options.
- Project/namespace/assembly/user-secrets identitet je preimenovan; ostaje domensko preimenovanje i preusmeravanje referentnih kontrolera.
- AutoMapper 12.0.1 prijavljuje poznatu high-severity ranjivost (`NU1903`); upgrade zahteva poseban dependency task.

