# Service.Api.Template - Preimenovanje

## Summary

Očišćeni API projekat sada koristi generički solution identitet `Service.Api.Template` i zamenljivi source token `Template.Service`. Svi aktivni projekti, namespace-i, reference-i, konfiguracija i testovi usklađeni su sa novim nazivom.

## Implemented Changes

- Solution je preimenovan u `Service.Api.Template.sln`.
- Glavni projekat je preimenovan u `Template.Service.Api`.
- Svi aplikacioni slojevi koriste prefiks `Template.Service`.
- DI projekat je preimenovan u `Template.Service.DependencyInjection`, uz ispravku starog typo-a.
- API test projekat je preimenovan u `Template.Service.Api.Tests`.
- Application name, root status identitet, launch profil, test audience/issuer primeri i user-secrets ID su generalizovani.
- Infrastrukturna dokumentacija i njene projektne putanje usklađene su sa novim identitetom.
- `Framework.Logger` je ostao generička infrastrukturna komponenta.
- Referentni CRM i Organizations kontroleri nisu domenski menjani.

## Affected Components

- `Service.Api.Template.sln` - solution identitet i project registry.
- `Template.Service.Api` - host i API infrastruktura.
- `Template.Service.*` projekti - svi aplikacioni slojevi i testovi.
- `docs/` - nazivi projekata, putanje, komande i odabrani nazivi dokumenata.

## Configuration / Deployment Impact

- Serilog application name je `Template.Service.Api`.
- Root status response koristi `Service.Api.Template`.
- Launch profile je `Template.Service.Api`.
- User-secrets ID je `Template.Service.Api-CrmClient`.
- Test authentication audience je `service-api-template`.
- Deployment pipeline nije menjan i mora koristiti novu solution/project putanju kada bude dodat ili prilagođen.

## Validation

- Restore: uspešan.
- Build: uspešan, 0 grešaka.
- API testovi: 74/74 uspešno.
- Framework.Logger testovi: 13/13 uspešno.
- Nema starih identiteta u aktivnom source-u.
- Sve `appsettings` varijante ostaju validan JSON.
- Završni high-confidence secret scan nije pronašao private key, cloud key, token ili connection-string obrasce.

## Known Limitations

- Spoljašnji workspace folder još nosi stari naziv i treba ga preimenovati izvan aktivne sesije.
- Stari `Web.Service.*` folderi i generisani `.vs`, `bin` i `obj` artefakti uklonjeni su nakon zatvaranja solution-a u Visual Studio-u.
- CRM i Organizations primeri još nose originalne domenske nazive i rute.
- Infrastrukturna dokumentacija je mehanički usklađena sa novim imenima, ali istorijske opise treba tumačiti u kontekstu očišćenog template-a.
- AutoMapper 12.0.1 i dalje generiše `NU1903` upozorenje.

## Further Improvements

- Dodati `.template.config/template.json` sa `sourceName` vrednošću `Template.Service`.
- Dodati README sa `dotnet new` instalacijom i korišćenjem.
- Preimenovati/preusmeriti referentne kontrolere nakon dogovora o novim primerima.
- Ažurirati AutoMapper u posebnom tasku.
