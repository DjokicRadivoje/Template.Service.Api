# Service.Api.Template GitHub Actions CI/CD - dopunjena analiza

Datum dopune: 2026-08-24

## Arhitektonska odluka: monorepo i service isolation

`RALF-AI-Services` ostaje monorepo sa vise nezavisnih API servisa:

```text
RALF-AI-Services/
|-- Service.Api.Template/
|-- HR.Api/
|-- Product.Api/
|-- ...
`-- .github/workflows/
```

Svaki API treba tretirati kao nezavisan deployable servis sa sopstvenim kodom, testovima, CI/CD tokom i Azure targetom. API servisi ne treba da dele source biblioteke unutar ovog repozitorijuma. Ako se zajednicka funkcionalnost bude delila izmedju API-ja, treba je distribuirati kao NuGet paket, a ne kao `ProjectReference` ka drugom API-ju ili zajednickom repo-local source projektu.

Za Service.Api.Template ciljni tok je:

```text
promena u Service.Api.Template/**
        |
        v
Service.Api.Template CI/CD
        |
        v
restore -> build -> test -> publish
        |
        v
raf-service-api-template-app
```

Promene u `HR.Api/**`, `Product.Api/**` ili drugom API folderu ne treba da pokrecu build niti deployment Service.Api.Template servisa.

## Trenutno stanje

Git root je:

```text
E:\GIT_2026\RALF-AI-Services
```

Workflow fajlovi trenutno postoje lokalno, ali nisu versionisani:

```text
?? .github/workflows/ci.yml
?? .github/workflows/deploy-dev.yml
```

### `.github/workflows/ci.yml`

- Pokrece se za `push` i `pull_request` na `main`, `development` i `staging`.
- Radi checkout, setup .NET 10, NuGet cache, restore, build i test.
- Trenutno koristi nepostojeci `working-directory: app`.
- NuGet cache hash koristi nepostojecu putanju `app/**/*.csproj`.
- Naziv `Template.Service.Api.sln` je validan, ali putanja nije validna iz `app` direktorijuma.

### `.github/workflows/deploy-dev.yml`

- Pokrece se samo za `development`.
- Path filteri `app/backend/**` i `app/shared/**` pripadaju prethodnom projektu.
- Koristi OIDC oblik `azure/login@v2` autentikacije.
- Restore i publish koraci ciljaju nepostojeci `app/backend/MinimalApi/MinimalApi.csproj`.
- Deployuje na stari App Service `ralf-adria-app-dev`.
- Nema `slot-name`, pa ne implementira zahtevani `development` named slot.
- Ne podrzava `staging`/UAT niti `main`/production deployment.

## Potvrdjena Service.Api.Template struktura

U trenutnom repozitorijumu ne postoji projekat doslovno nazvan `Service.Api.Template.csproj`. Deployable web projekat je:

```text
Service.Api.Template/Template.Service.Api/Template.Service.Api.csproj
```

Jedini solution je:

```text
Service.Api.Template/Template.Service.Api.sln
```

`Template.Service.Api.sln` zato nije stara ili nevalidna referenca sama po sebi. Problem je njegova upotreba iz nepostojeceg `working-directory: app`. Iz repo root-a treba koristiti `Service.Api.Template/Template.Service.Api.sln`, ili service-scoped working directory `Service.Api.Template`.

API i svi projekti u solution-u targetuju `net10.0`. `Service.Api.Template/global.json` zahteva SDK `10.0.400`, uz `latestPatch` roll-forward.

Test projekti se nalaze unutar `Service.Api.Template/`:

```text
Service.Api.Template/Template.Service.Api.Tests/Template.Service.Api.Tests.csproj
Service.Api.Template/Framework.Logger.Tests/Framework.Logger.Tests.csproj
```

Oba test projekta su ukljucena u `Service.Api.Template/Template.Service.Api.sln`.

## ProjectReference i granice servisa

Svi trenutni `ProjectReference` odnosi su servis-interni. Sve referencirane biblioteke nalaze se ispod `Service.Api.Template/`, ukljucujuci:

```text
Service.Api.Template/Framework.Logger/
Service.Api.Template/Template.Service.DependencyInjection/
Service.Api.Template/Template.Service.BusinessLogic/
Service.Api.Template/Template.Service.BusinessModel/
Service.Api.Template/Template.Service.DataModel/
Service.Api.Template/Template.Service.BusinessLogic.Interfaces/
Service.Api.Template/Template.Service.Services.Interfaces/
Service.Api.Template/Template.Service.Mapper/
Service.Api.Template/Template.Service.Services/
```

Nema `ProjectReference` ka `HR.Api`, `Product.Api`, root `Shared`, `Common` ili `Framework` projektu. Zbog toga Service.Api.Template workflow ne treba da ima path filtere za druge servise ili hipoteticke shared foldere.

## Nezavisni restore/build/test/publish

Iz repo root-a provereni su service-scoped restore, build, test i publish koraci.

Rezultat:

```text
restore: uspesan sa eksplicitnom Release konfiguracijom
build: uspesan, 0 errors, 10 postojecih upozorenja
Framework.Logger.Tests: 12/12 passed
Template.Service.Api.Tests: 82 passed, 3 failed
publish Template.Service.Api.csproj: uspesan
```

Service.Api.Template moze da se restore-uje, builduje i publishuje bez projekta izvan `Service.Api.Template/`. Direktan publish web projekta automatski ukljucuje njegove servis-interne project reference zavisnosti.

Kompletan test suite trenutno nije zelen. Pala su tri postojeca testa:

```text
DependencyInjectionConfigTests.ConfigureHttpClients_MissingClientSecret_ThrowsWithoutExposingAValue
KeycloakAuthenticationIntegrationTests.ProductController_WithoutToken_ReturnsUnauthorized
KeycloakAuthenticationIntegrationTests.CrmController_WithoutToken_ReturnsUnauthorized
```

Ovi test fail-ovi ne menjaju service-isolation zakljucak, ali moraju biti reseni ili potvrđeni na cistom GitHub runner-u pre nego sto se deployment uslovi obaveznim zelenim test gate-om.

Na lokalnoj masini postoji ambient varijabla:

```text
CONFIGURATION=configuration-service/
```

Zbog nje `dotnet restore` bez eksplicitne konfiguracije pokusava nepostojecu solution konfiguraciju. Validacija je uspela kada je eksplicitno zadat `Release`. GitHub workflow treba eksplicitno da koristi `Release`, kao sto ciljni tok vec zahteva.

Postojeca upozorenja ukljucuju `NU1903` za AutoMapper 12.0.1 i nullable upozorenja. Ona nisu nastala CI/CD analizom, ali treba da ostanu vidljiva u CI logu.

## NuGet izvori i privatni feed

U repozitorijumu trenutno ne postoje:

```text
NuGet.config
nuget.config
Directory.Packages.props
packages.lock.json
```

Svi trenutni `PackageReference` paketi su javni. Lokalno registrovani izvori su:

```text
nuget.org
Microsoft Visual Studio Offline Packages
```

GitHub-hosted runner ce standardno koristiti `nuget.org`. Za trenutno stanje nije potrebna autentikacija privatnog NuGet feed-a.

Ako se buduca zajednicka funkcionalnost distribuira kao privatni NuGet paket, tada treba eksplicitno dodati NuGet source/authentication konfiguraciju i koristiti odgovarajuci GitHub/Azure secret za token. Ne treba unapred uvoditi autentikaciju za feed koji trenutno ne postoji.

## Problemi / stare reference

### `.github/workflows/ci.yml`

- Linija 32: ukloniti `working-directory: app` ili ga promeniti u `Service.Api.Template`.
- Linija 58: promeniti `hashFiles('app/**/*.csproj')` u service-scoped Service.Api.Template hash.
- Linije 68, 75 i 84: ako nema service working directory-ja, koristiti `Service.Api.Template/Template.Service.Api.sln`.

### `.github/workflows/deploy-dev.yml`

- Linije 7-9: ukloniti stare `app/backend/**` i `app/shared/**` filtere.
- Linija 18: ukloniti neiskoriscen `ENVIRONMENT_NAME: dev`; workflow ne treba da postavlja Azure runtime environment.
- Linija 54: ukloniti stari `MinimalApi.csproj` restore target.
- Linije 60-63: publishovati stvarni Service.Api.Template web projekat.
- Linija 71: zameniti `ralf-adria-app-dev` sa `raf-service-api-template-app`.
- Linije 80-82: ukloniti ili korigovati status proveru koja koristi stari App Service.
- Dodati podrsku za `main`, `development` i `staging` sa pravilnim slot mapping-om.

## Predlozena ciljna struktura

Za trenutno stanje preporucuje se jedan service-scoped workflow:

```text
.github/workflows/service-api-template-ci-cd.yml
```

Logicno moze imati dva job-a:

```text
build-test-publish
        |
        v
      deploy
```

PR izvrsava CI deo bez Azure autentikacije i deploymenta. Push na dozvoljene deployment grane izvrsava isti CI, publishuje jedan izolovan artifact i tek zatim pokrece deploy job.

Preporuceni trigger:

```yaml
on:
  push:
    branches:
      - main
      - development
      - staging
    paths:
      - 'Service.Api.Template/**'
      - '.github/workflows/service-api-template-ci-cd.yml'

  pull_request:
    branches:
      - main
      - development
      - staging
    paths:
      - 'Service.Api.Template/**'
      - '.github/workflows/service-api-template-ci-cd.yml'
```

Ne dodavati sledece path filtere bez stvarne zavisnosti:

```text
Shared/**
Common/**
Framework/**
HR.Api/**
Product.Api/**
```

Ako kasnije bude vise servisa, svaki moze imati sopstveni thin workflow:

```text
.github/workflows/
|-- service-api-template-ci-cd.yml
|-- hr-api-ci-cd.yml
`-- product-api-ci-cd.yml
```

Reusable workflow treba razmotriti tek kada se pojavi znacajno stvarno dupliranje. Ne treba ga uvoditi unapred dok postoji samo Service.Api.Template CI/CD.

## CI i publish flow

Preporuceni redosled:

```text
checkout
setup .NET 10
restore Service.Api.Template/Template.Service.Api.sln
build Service.Api.Template/Template.Service.Api.sln -c Release --no-restore
test Service.Api.Template/Template.Service.Api.sln -c Release --no-build --no-restore
publish Service.Api.Template/Template.Service.Api/Template.Service.Api.csproj -c Release -f net10.0
upload samo publish output
```

Publish treba izvrsiti direktno nad:

```text
Service.Api.Template/Template.Service.Api/Template.Service.Api.csproj
```

a ne nad celim solution-om kao deployment artifactom.

## Publish artifact

Provereni publish output ne sadrzi:

```text
.git/
.vs/
bin/
obj/
*.cs source fajlove
lokalni User Secrets storage
```

Sadrzi ocekivane DLL/PDB/runtime fajlove, `web.config`, `appsettings.json` i `appsettings.Development.json`.

PDB fajlovi nisu source kod, ali mogu opciono biti iskljuceni kasnijom publish konfiguracijom ako organizaciona praksa zahteva manji production artifact.

`appsettings.Development.json` ne sadrzi client secret. Azure slot App Settings imaju visi prioritet nad JSON vrednostima. Workflow ne treba da kopira lokalni User Secrets storage niti da postavlja runtime secret vrednosti.

## Deploy flow i branch mapping

Azure target ostaje:

```text
App Service: raf-service-api-template-app
Resource Group: Chatbot_RG
```

Mapping:

```text
main        -> default production slot -> PROD
development -> development slot        -> DEV
staging     -> staging slot             -> UAT
```

Za `main` deploy korak ne treba da prosledi `slot-name`. Za `development` i `staging` treba koristiti named slot ciji naziv odgovara branch-u.

Jedan workflow moze pouzdano implementirati mapping sa dva conditional deploy koraka:

- production korak samo za `main`, bez `slot-name`;
- named-slot korak za `development` i `staging`, sa `slot-name: ${{ github.ref_name }}`.

Workflow ne treba da postavlja:

```text
Azure App Settings
ASPNETCORE_ENVIRONMENT
Key Vault reference
Key Vault secret vrednosti
connection stringove
```

Azure environment konfiguracija vec postoji izvan workflow-a.

## Azure autentikacija

Postojeci deploy workflow koristi:

```yaml
permissions:
  contents: read
  id-token: write

- uses: azure/login@v3
  with:
    client-id: ${{ secrets.AZURE_CLIENT_ID }}
    tenant-id: ${{ secrets.AZURE_TENANT_ID }}
    subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
```

Ovo je OIDC oblik autentikacije. Ne koristi Service Principal credential JSON, `client-secret` niti publish profile.

Repo ne moze potvrditi da secrets postoje, da Entra federated credential pokriva sve tri grane/GitHub environmenta, niti da deployment identity ima potreban RBAC nad aplikacijom i named slotovima.

App Service Managed Identity za runtime pristup Key Vault-u nije isto sto i GitHub OIDC deployment identity. GitHub deployment identity mora odvojeno imati potreban App Service deployment pristup.

## Konkretne izmene

### `.github/workflows/ci.yml`

Ukloniti:

```text
defaults.run.working-directory: app
hashFiles('app/**/*.csproj')
```

Promeniti:

```text
restore/build/test target -> Service.Api.Template/Template.Service.Api.sln
NuGet cache scope -> Service.Api.Template/**/*.csproj i Service.Api.Template/global.json
```

Dodati ili preneti u novi `service-api-template-ci-cd.yml`:

```text
Service.Api.Template/** path filter
publish stvarnog web projekta
upload/download publish artifact
deploy job samo za push
OIDC login u deploy job-u
branch-to-slot mapping
raf-service-api-template-app target
```

### `.github/workflows/deploy-dev.yml`

Kada se funkcionalnost objedini u `service-api-template-ci-cd.yml`, ukloniti stari workflow da ne postoje dupli triggeri. Ako se zadrzava odvojeni CD fajl, mora se potpuno prepraviti za stvarne Service.Api.Template putanje, sva tri branch-a i slot mapping.

## Otvorena pitanja

Iz repozitorijuma se ne moze potvrditi:

- Da li `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` i `AZURE_SUBSCRIPTION_ID` vec postoje i imaju ispravne vrednosti.
- Da li Entra federated credential pokriva ovaj repo i sve potrebne grane ili GitHub Environments.
- Da li deployment identity ima odgovarajuci RBAC nad `raf-service-api-template-app`, `development` i `staging` slotovima.
- Da li GitHub Environments `production`, `development` i `staging` postoje i da li production zahteva approval.
- Da li remote grane `development` i `staging` vec postoje; trenutni lokalni clone vidi samo `origin/main`.
- Da li tri pala API testa prolaze na cistom GitHub-hosted runner-u ili zahtevaju korekciju test izolacije.

## Deployment readiness checklist

Korake izvrsavati redom. Ne prelaziti na sledeci korak dok trenutni nije potvrđen.

- [x] 1. Kreirati jedan service-scoped `service-api-template-ci-cd.yml` i ukloniti stare workflow fajlove.
- [x] 2. Validirati YAML sintaksu, stvarne solution/project putanje, .NET SDK rezoluciju, restore, build i publish komande.
- [x] 3. Utvrditi uzrok tri pala `Template.Service.Api.Tests` testa i obezbediti zelen kompletan test suite.
- [ ] 4. Potvrditi da su GitHub Actions omogucene za repo i da repository/organization secrets `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` i `AZURE_SUBSCRIPTION_ID` postoje.
- [ ] 5. Potvrditi Entra federated credential za ovaj GitHub repo i odgovarajuce branch ref-ove (`main`, `development`, `staging`) ili uskladiti workflow sa GitHub Environment subject-ima.
- [ ] 6. Potvrditi da GitHub OIDC deployment identity ima potreban RBAC nad `raf-service-api-template-app` i named slotovima `development` i `staging`.
- [ ] 7. Potvrditi ili kreirati remote `development` i `staging` grane.
- [ ] 8. Commitovati i pushovati workflow prvo na `development`; potvrditi da CI prolazi i da je deployment zavrsen na `development` slotu.
- [ ] 9. Izvrsiti DEV smoke test nad deployovanom aplikacijom bez menjanja Azure App Settings ili Key Vault konfiguracije.
- [ ] 10. Promovisati isti provereni workflow na `staging`; potvrditi UAT deployment i smoke test.
- [ ] 11. Tek posle DEV/UAT potvrde merge-ovati ili pushovati na `main`; potvrditi deployment na default production slot bez `slot-name`.
- [ ] 12. Posle prvog uspesnog production deploymenta potvrditi App Service health/logove i dokumentovati rezultat.

Trenutni aktivni korak: **4 - potvrda GitHub Actions i OIDC secret konfiguracije**.

### Rezultat koraka 4 - novi Service.Api.Template App Registration, 2026-08-25

- Kreiran je Entra App Registration `ralf-ai-bapi-mk-action`.
- Application (client) ID za GitHub secret `AZURE_CLIENT_ID`: `94059648-fc5b-43e5-8210-91f6255f79f8`.
- Directory (tenant) ID za GitHub secret `AZURE_TENANT_ID`: `719dc930-9d0e-4ea4-b53e-a2c65a625979`.
- Object ID `ad047544-8a6b-4568-8737-d2f0dd20a995` se ne prosledjuje `azure/login` akciji.
- `AZURE_SUBSCRIPTION_ID` nije vidljiv na dostavljenom snimku i jos mora biti potvrdjen.
- Workflow zadrzava identifikatore u GitHub secrets i ne hardkoduje ih u YAML; `azure/login` je azuriran na v3.
- Korak 4 ostaje otvoren dok se ne potvrdi da sva tri GitHub secrets postoje sa odgovarajucim vrednostima.
- Sledeci zaseban uslov je federated identity credential za GitHub OIDC issuer/subject, a zatim RBAC nad App Service aplikacijom i slotovima.

### Rezultat koraka 3 - Test gate patch, 2026-08-24

Uzrok tri pada uveden je commitom `c217c0c`:

- `ProductController` i `CrmController` dobili su `[AllowAnonymous]`, suprotno postojecoj globalnoj JWT fallback politici i dokumentovanoj odluci da poslovni endpoint-i zahtevaju autentifikaciju;
- fail-fast exception za nedostajuci `KeycloakClient:ClientSecret` zakomentarisan je, iako je client secret obavezna runtime konfiguracija iz secret provider-a.

Patch je vratio prethodno dokumentovano sigurnosno ponasanje:

- uklonjen je `[AllowAnonymous]` sa oba poslovna controller-a;
- vracena je fail-fast validacija za nedostajuci client secret;
- secret vrednost se ne prikazuje u exception poruci.

Validacija:

```text
Ciljana tri regresiona testa: 3/3 passed
Template.Service.Api.Tests: 85/85 passed
Framework.Logger.Tests: 12/12 passed
Ukupno: 97/97 passed
```

## Status izmena

- Implementirano 2026-08-24: stari `.github/workflows/ci.yml` i `.github/workflows/deploy-dev.yml` zamenjeni su jednim `.github/workflows/service-api-template-ci-cd.yml` fajlom.
- Novi workflow je service-scoped na `Service.Api.Template/**` i sopstvenu workflow putanju.
- CI job koristi `Service.Api.Template` kao radni direktorijum, `Service.Api.Template/global.json` za SDK rezoluciju, stvarni solution i stvarni web projekat.
- PR radi restore/build/test bez deploymenta; push na `main`, `development` ili `staging` dodatno radi publish, artifact transfer, OIDC login i deployment.
- `main` se deployuje na default production slot bez `slot-name`; `development` i `staging` koriste named slot jednak nazivu branch-a.
- YAML sintaksa je validirana komandom `npx --yes yaml-lint .github/workflows/service-api-template-ci-cd.yml`.
- Tacna workflow restore komanda je lokalno potvrđena sa .NET SDK `10.0.400`; restore je uspesan uz postojeca `NU1903` upozorenja.
- Build validacija je uspesna sa 0 gresaka; posle sigurnosnog regression patch-a kompletan test suite prolazi `97/97` i test gate vise nije lokalni blocker.
- Azure App Settings i Key Vault konfiguracija nisu menjani.
- Azure OIDC secret vrednosti nisu upisivane niti proveravane lokalno.

