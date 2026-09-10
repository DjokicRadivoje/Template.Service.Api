# API Template Cleanup

## Summary

Business API kopija je očišćena do generičke infrastrukturne osnove sa dva postojeća referentna poslovna toka: CRM mock i Organizations search. Njihove rute i ponašanje ostali su nepromenjeni, dok su ostali domeni i njihove isključive zavisnosti uklonjeni.

## Implemented Changes

- Zadržani su `CrmController` i `OrganizationsController`.
- Uklonjeno je osam ostalih poslovnih kontrolera.
- Business logic, service, interface, model i mapper slojevi svedeni su na dva zadržana toka i zajedničke infrastrukturne ugovore.
- `CrmService` i `ICrmService` sada izlažu samo `GetMockData` i `GetOrganizations`.
- DI sadrži samo registracije zadržanih tokova i njihove HTTP/authentication infrastrukture.
- Test suite je očišćen i zadržava testove referentnih tokova i infrastrukture.
- Environment konfiguracija je sanitizovana i koristi template/example vrednosti.
- Dodat je `.gitignore` za build, IDE, log i lokalne secret artefakte.

## Affected Components

- `Template.Service.Api` - dva kontrolera, API infrastruktura i sanitizovana konfiguracija.
- `Template.Service.BusinessLogic` / `Template.Service.BusinessLogic.Interfaces` - CRM i Organizations tokovi.
- `Template.Service.Services` / `Template.Service.Services.Interfaces` - CRM integracija, HTTP response handling i CRM client authentication.
- `Template.Service.BusinessModel` / `Template.Service.DataModel` - common, CRM i Organizations ugovori.
- `Template.Service.Mapper` - CRM mape.
- `Template.Service.DependencyInjection` - fokusirana DI i named HttpClient konfiguracija.
- `Template.Service.Api.Tests` / `Framework.Logger.Tests` - referentni i infrastrukturni testovi.

## Configuration / Deployment Impact

Uklonjeni su konfiguracioni ključevi za Product, HR, Accounting, subscriptions i generički outbound Keycloak client. Ostaju:

- `Authentication:Keycloak:*` za inbound JWT validaciju;
- `ApiPaths:CRMApiBaseAddress`;
- `CrmClient:ClientId`;
- `CrmClient:ClientSecret`, koji mora doći iz user-secrets/environment/secret provider-a;
- `CrmClient:RefreshBeforeExpirySeconds`.

Source konfiguracija ne sadrži CRM client secret. Environment URL-ovi koriste rezervisani `example.invalid` domen i moraju se override-ovati u konkretnom projektu.

Nisu menjani deployment pipeline-i. Postojeća istorijska CI/CD dokumentacija još nije prilagođena template-u.

## Validation

- Solution build: uspešan, 0 grešaka.
- API testovi: 74/74 uspešno.
- Framework.Logger testovi: 13/13 uspešno.
- AutoMapper configuration test: uspešan.
- DI resolution testovi zadržanih tokova: uspešni.
- Nema source referenci prema uklonjenim poslovnim domenima.
- Heuristička credential i URL provera source/config fajlova nije pronašla stvarne credential-e ili interne URL-ove.

## Known Limitations

- AutoMapper 12.0.1 generiše `NU1903` upozorenje zbog poznate high-severity ranjivosti.
- Zadržana infrastrukturna dokumentacija je preimenovana, ali istorijske opise treba sadržajno proveravati u odnosu na očišćeni template; identifikovani interni Atlassian linkovi su uklonjeni.
- `.vs`, `bin` i `obj` direktorijumi fizički postoje, ali su isključeni novim `.gitignore` fajlom.
- Root status endpoint i health check još dele `/` rutu.
- Root workspace folder još nosi stari naziv jer nije bezbedno preimenovati aktivni workspace iz njega samog.
- `RoleClientId` postoji u inbound authentication options, ali nije korišćen u trenutnoj authorization konfiguraciji.

## Further Improvements

- Domensko preimenovanje i preusmeravanje referentnih kontrolera.
- Template README sa uputstvom za secrets, konfiguraciju, pokretanje i kreiranje novog business toka.
- Poseban dependency upgrade za AutoMapper.
- Odvajanje status i health-check ruta.
- Finalni secret scan neposredno pre prvog `git add`/commita.

