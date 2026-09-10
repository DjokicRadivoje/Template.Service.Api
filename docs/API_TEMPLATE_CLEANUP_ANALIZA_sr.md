# API Template Cleanup - Analiza

## Context

Stabilna kopija projekta `Service.Api.Template` izdvojena je van Git repozitorijuma sa ciljem da postane početni template za buduće API projekte. Template treba da zadrži zajedničku infrastrukturnu osnovu i dva postojeća poslovna toka kao primere implementacije:

- `CrmController`;
- `OrganizationsController`.

U prvom krugu ponašanje, rute i nazivi ova dva kontrolera ne treba da se menjaju. Preostali poslovni kontroleri i kod koji postoji isključivo zbog njih treba ukloniti. Pre prvog Git commita potrebno je ukloniti ili generalizovati interne vrednosti i proveriti da nijedan credential nije ostao u projektu.

## Current State

Solution sadrži deset produkcionih kontrolera, od kojih su dva odabrana kao referentni primeri. Posebni infrastrukturni kontroleri trenutno ne postoje. Infrastrukturni endpoint-i su registrovani kao minimal API/health-check endpoint-i u `Program.cs`.

Projekat trenutno nije Git repozitorijum. U radnom folderu postoje `.vs`, `bin` i `obj` artefakti, ne postoji korenski `.gitignore`, a pronađeno je 25 direktorijuma sa nazivom `.vs`, `bin` ili `obj`.

Postojeća struktura razdvaja:

- API kontrolere i API infrastrukturu;
- business modele;
- business logic interfejse i implementacije;
- servisne interfejse i implementacije;
- data modele;
- AutoMapper profil;
- Autofac/DI konfiguraciju;
- zajednički correlation/logging framework;
- API i framework testove.

## Target State

Prvi cleanup treba da proizvede buildabilan i testabilan template koji:

- zadržava `CrmController` i `OrganizationsController` bez promene njihovih javnih ruta i ponašanja;
- zadržava samo njihove potrebne business, service, model, mapper, HTTP i authentication zavisnosti;
- zadržava generičku API infrastrukturu: inbound autentifikaciju/autorizaciju, Swagger, response status filter, correlation ID, telemetry, logging i health check;
- uklanja ostale poslovne kontrolere i njihove isključive zavisnosti;
- uklanja testove obrisanih tokova, ali zadržava infrastrukturne i testove dva primera;
- može da se pripremi za prvi commit bez build artefakata, lokalnih IDE fajlova, credential-a i internih konfiguracionih vrednosti;
- ne uvodi preimenovanje ili preusmeravanje referentnih kontrolera u ovom krugu.

## Affected Components

### Kontroleri koji ostaju

- `Template.Service.Api/Controllers/CrmController.cs` - jednostavan primer Controller -> BL -> AutoMapper -> HTTP service toka.
- `Template.Service.Api/Controllers/OrganizationsController.cs` - potpuniji primer validacije, paginacije, CRM error mapiranja i oblikovanja odgovora.

### Kontroleri kandidati za uklanjanje

- `AccountingEventsController`;
- `AccountingInvoicesController`;
- `CustomersController`;
- `CustomerSubscriptionsController`;
- `ProductController`;
- `TrainingAttendanceEventsController`;
- `TrainingProductsController`;
- `TrainingRegistrationsController`.

### Infrastruktura koja ostaje

- `Template.Service.Api/Infrastructure/Authentication` - inbound Keycloak/JWT autentifikacija i fallback authorization policy;
- `Template.Service.Api/Infrastructure/Http` - mapiranje standardnog `Response<T>` statusa na HTTP status;
- `Template.Service.Api/Infrastructure/Telemetry` - correlation ID integracija sa Application Insights;
- `Framework.Logger/Correlation` i pripadajući testovi;
- Swagger/OpenAPI, Serilog, Application Insights i health checks iz `Program.cs`;
- `Template.Service.BusinessModel/Common` response ugovori, uz uklanjanje domenski specifičnih message code konstanti;
- `HttpResponseHandler` i `CrmResponseHandler`, jer ih koriste zadržane CRM metode;
- CRM outbound authentication infrastruktura (`CrmAccessTokenProvider`, `CrmBearerTokenHandler` i njihovi ugovori/opcije).

## Dependency Flow zadržanih primera

### CrmController

```text
CrmController
  -> ICrmBusinessLogic
  -> CrmBusinessLogic
  -> ICrmService
  -> CrmService.GetMockData
  -> IHttpResponseHandler / HttpResponseHandler
  -> CRM named HttpClient
  -> CrmBearerTokenHandler / ICrmAccessTokenProvider
```

Potrebni modeli i mape:

- `Template.Service.BusinessModel/Crm`;
- `Template.Service.DataModel/Crm/CrmRequest.cs`;
- `Template.Service.DataModel/Crm/CrmResponse.cs`;
- dve CRM mape u `DefaultProfile`.

### OrganizationsController

```text
OrganizationsController
  -> IOrganizationBusinessLogic
  -> OrganizationBusinessLogic
  -> BusinessLogicResponseHelper
  -> CrmErrorMapper
  -> ICrmService
  -> CrmService.GetOrganizations
  -> ICrmResponseHandler / CrmResponseHandler
  -> CRM named HttpClient
  -> CrmBearerTokenHandler / ICrmAccessTokenProvider
```

Potrebni modeli:

- `Template.Service.BusinessModel/Organizations`;
- `Template.Service.DataModel/Organizations`;
- `Template.Service.BusinessModel/Common`;
- `Template.Service.DataModel/Crm/CrmApiResponse.cs` i `CrmErrorResponse.cs`, posredno kroz `CrmResponseHandler`/`CrmServiceResult`.

`OrganizationsController` ne koristi AutoMapper za organization modele; mapiranje se trenutno obavlja eksplicitno u `OrganizationBusinessLogic`.

## Analysis

### Zajednički CrmService zahteva parcijalno čišćenje

`CrmService` i `ICrmService` nisu ograničeni na dva zadržana toka. Oni sadrže metode i imports za customers, subscriptions, accounting events, training products, training registrations i attendance events.

Zbog toga nije moguće istovremeno:

1. ostaviti cele klase `CrmService` i `ICrmService` doslovno neizmenjene; i
2. ukloniti modele ostalih poslovnih tokova.

Preporučeno tumačenje zahteva je:

- javno ponašanje, rute i ugovori dva kontrolera ostaju nepromenjeni;
- u `CrmService` i `ICrmService` ostaju samo `GetMockData` i `GetOrganizations`;
- nepovezane metode, putanje i `using` direktive uklanjaju se zajedno sa pripadajućim modelima i testovima.

Ovo je najmanja izmena koja omogućava stvarno kaskadno čišćenje bez promene ponašanja dva primera.

### Business logic sloj

Za zadržavanje su potrebni:

- `CrmBusinessLogic` i `ICrmBusinessLogic`;
- `OrganizationBusinessLogic` i `IOrganizationBusinessLogic`;
- `BusinessLogicResponseHelper`;
- `Infrastructure/CrmErrorMapper`.

Ostale business logic klase i njihovi interfejsi postaju sigurno uklonjivi nakon uklanjanja pripadajućih kontrolera i skraćivanja zajedničkog CRM ugovora.

### Servisni sloj

`ProductService`/`IProductService` i `AccountingService`/`IAccountingService` služe samo tokovima predviđenim za uklanjanje. Njihovi named HTTP klijenti i outbound Keycloak client-credentials infrastruktura tada više nemaju produkcionog potrošača.

CRM servis ostaje, ali se svodi na dve metode zadržanih primera. Oba HTTP response handler-a ostaju zato što svaki od dva primera koristi različit handler.

### AutoMapper

`DefaultProfile` trenutno sadrži CRM, product, customer, subscription, accounting i training mape. Za zadržane kontrolere direktno su potrebne samo:

- `CrmRequest` -> data `CrmRequest`;
- data `CrmResponse` -> business `CrmResponse`.

Generička `PagedResponse<>` mapa trenutno nije korišćena u Organizations toku, jer se rezultat mapira ručno. Može se ukloniti ako završna pretraga i testovi ne pokažu drugu runtime upotrebu.

### Dependency injection i HTTP klijenti

Iz `ConfigureContainer` treba zadržati registracije za:

- `CrmBusinessLogic`;
- `CrmService`;
- `OrganizationBusinessLogic`;
- `HttpResponseHandler`;
- `CrmResponseHandler`.

Registracije drugih BL i servisnih klasa kandidati su za uklanjanje.

Iz `ConfigureHttpClients` CRM API i CRM token klijenti moraju ostati. Product, HR i Accounting klijenti postaju nepotrebni kada se uklone njihovi tokovi.

`KeycloakAccessTokenProvider`, `BearerTokenHandler`, `IAccessTokenProvider`, `KeycloakClientOptions` i `KeycloakToken` named client služe outbound Accounting autentifikaciji i nisu isto što i inbound JWT zaštita API-ja. Nakon uklanjanja Accounting toka mogu se ukloniti, dok `AddKeycloakAuthentication` i `Authentication:Keycloak` konfiguracija ostaju.

`SubscriptionIntegrationOptions` i `SourceSystem` konfiguracija postaju nepotrebni nakon uklanjanja subscription toka.

### Testovi

Testovi koji ostaju bez konceptualne promene:

- `Controllers/CrmControllerTests.cs`;
- infrastrukturni authentication, HTTP i telemetry testovi koji se odnose na zadržanu infrastrukturu;
- `Framework.Logger.Tests`.

Mešoviti test fajlovi moraju se parcijalno očistiti:

- `Controllers/ReferenceControllersTests.cs` - zadržati Organizations testove i njihov stub, ukloniti testove ostalih kontrolera;
- `BusinessLogic/CrmBusinessLogicFlowTests.cs` - zadržati Organization BL testove, ukloniti ostale poslovne scenarije i njihove stubove/helper-e;
- `Services/CrmServiceTests.cs` - zadržati CRM mock i Organizations servisne testove, ukloniti testove metoda koje se brišu;
- `DependencyInjection/DependencyInjectionConfigTests.cs` - zadržati CRM/Organizations i infrastrukturne provere, ukloniti očekivanja za obrisane registracije i konfiguraciju;
- `DependencyInjection/NamedHttpClientAuthenticationTests.cs` - prilagoditi da proverava samo CRM klijente i CRM autentifikaciju;
- `Mapping/DefaultProfileTests.cs` - ostaje, ali nakon uklanjanja mapa mora potvrditi novu, manju konfiguraciju.

Fajlovi `ProductControllerTests.cs`, `ProductServiceTests.cs` i `AccountingServiceTests.cs` postaju u celosti uklonjivi.

Postojeći `CrmBusinessLogic` trenutno nema direktan unit test za svoj uspešan/neuspešan tok osim kontrolerskog testa i DI resolution provere. Za kvalitet referentnog primera preporučljivo je dodati ciljane BL testove tokom cleanup-a ili ih evidentirati kao follow-up ako je cilj strogo samo uklanjanje.

### Infrastrukturni endpoint-i

Produkcioni projekat nema zasebne infrastrukturne kontrolere. `Program.cs` mapira root status endpoint i health check na istu putanju `/`. Ovo treba ručno proveriti tokom template generalizacije jer dva endpoint-a na istoj putanji mogu da proizvedu nejasno ili konfliktno rutiranje. Nije potrebno menjati u prvom cleanup koraku ako želimo strogo očuvanje postojeće infrastrukture.

### Dokumentacija

Postojeći `docs/` sadrži istoriju Business API implementacije, RAF/RSM termine, CI/CD analizu i više SR/EN/MK varijanti. Veliki deo nije prikladan za generički template čak i kada ne sadrži credential-e.

Pre prvog commita dokumentaciju treba klasifikovati odvojeno:

- zadržati i eventualno sažeti dokumente koji objašnjavaju generičku infrastrukturu;
- ukloniti dokumente koji postoje samo kao istorija obrisanih poslovnih zahteva;
- sanitizovati interne nazive sistema, okruženja i resursa;
- napraviti novi template README sa uputstvom za preimenovanje, konfiguraciju, secrets i pokretanje.

Ovaj dokument ne predlaže automatsko brisanje stare dokumentacije u prvom source cleanup prolazu bez posebne potvrde.

### Konfiguracija i credential provera

Pregled JSON strukture bez ispisivanja vrednosti pokazao je da environment konfiguracije sadrže:

- interne ili environment-specifične URL vrednosti koje zahtevaju pregled;
- konkretne audience, role client i client ID vrednosti;
- `ApiPaths` ključeve za Product, HR, CRM i Accounting;
- `SourceSystem`;
- CRM client ID placeholder vrednosti;
- bez upisanih `ClientSecret` vrednosti u pregledanim source JSON fajlovima.

`ClientSecret` se zahteva u runtime konfiguraciji, ali nije prisutan u source `appsettings` fajlovima, što ukazuje na user-secrets/environment provider pristup. To je dobro, ali nije dovoljan dokaz da u drugim tekstualnim ili generisanim fajlovima nema osetljivih vrednosti.

Dokumentacija sadrži brojne reference na secret/configuration ključeve. Te reference nisu automatski stvarni credential-i, ali svaki označeni red zahteva sadržajni pregled pre Git inicijalizacije.

Nisu pronađeni `.env`, certificate/private-key ili user fajlovi izvan već isključenih build/IDE direktorijuma. Potrebna je završna specijalizovana secret provera nakon cleanup-a, bez oslanjanja samo na `xxx` placeholder obrazac.

### Template identitet

Projekat i dalje nosi identitet originalnog sistema kroz solution/project naziv, namespace, `UserSecretsId`, Swagger assembly metadata, Serilog application name, root status response, environment nazive i test podatke. To nije deo prvog cleanup-a kontrolera, ali mora biti poseban drugi krug pre objavljivanja template-a.

## Cleanup Candidates

### Definitely used

- `CrmController`, `ICrmBusinessLogic`, `CrmBusinessLogic`;
- `OrganizationsController`, `IOrganizationBusinessLogic`, `OrganizationBusinessLogic`;
- `ICrmService`/`CrmService` za dve zadržane metode;
- CRM business/data modeli potrebni za `GetMockData`;
- Organizations business/data modeli;
- common response modeli;
- `BusinessLogicResponseHelper` i `CrmErrorMapper`;
- CRM HTTP client, CRM token provider/handler i njihove opcije/interfejsi;
- `HttpResponseHandler`, `CrmResponseHandler` i njihovi ugovori;
- inbound Keycloak authentication, response-status, telemetry, correlation i logging infrastruktura.

### Safe to remove nakon skraćivanja zajedničkih ugovora

- osam nezadržanih poslovnih kontrolera;
- odgovarajuće BL implementacije i interfejsi;
- `ProductService`, `AccountingService` i njihovi interfejsi;
- business/data model folderi za Accounting, Customers, Product, Subscriptions i Training;
- `Template.Service.DataModel/Employee`;
- mape za obrisane domene;
- DI registracije uklonjenih tipova;
- Product, HR i Accounting named HTTP klijenti i konfiguracioni ključevi;
- outbound `KeycloakAccessTokenProvider`/`BearerTokenHandler` tok koji je vezan za Accounting klijent;
- `SubscriptionIntegrationOptions` i `SourceSystem`;
- testovi ili delovi testova koji postoje samo zbog uklonjenih tokova.

### Manual review required

- generička `PagedResponse<>` AutoMapper mapa;
- `RoleClientId` u inbound Keycloak options, jer trenutno nije korišćen u produkcionoj autorizacionoj konfiguraciji;
- preklapanje root status i health-check rute `/`;
- kompletan postojeći `docs/` sadržaj;
- svi environment URL-ovi, client/application identifikatori i nazivi resursa;
- build/IDE artefakti pre prvog commita;
- template identitet i drugi krug preimenovanja;
- konačan secret scan celog očišćenog foldera.

## Risks

- Preuranjeno brisanje CRM modela može pokvariti `CrmResponseHandler`, koji koristi CRM envelope/error ugovore posredno.
- Brisanje celog `CrmService` testa umesto parcijalnog čišćenja uklonilo bi pokrivenost oba referentna toka.
- Mešoviti test fajlovi imaju veliki broj zajedničkih stubova; mehaničko brisanje blokova može ostaviti compile-time reference prema uklonjenim modelima.
- Uklanjanje outbound Keycloak client infrastrukture ne sme ukloniti inbound JWT authentication infrastrukturu sličnog naziva.
- Konfiguracioni cleanup mora ostaviti sve ključeve potrebne da se CRM primer registruje i DI graph izgradi.
- Placeholder `x` vrednosti mogu zadovoljiti neke `IsNullOrWhiteSpace` validacije, ali ne garantuju da je konfiguracija semantički bezbedna ili pokretljiva.
- Build output može sadržati kopije ranijih konfiguracionih fajlova; zato se `bin`/`obj` ne smeju unositi u Git niti koristiti kao izvor istine za sanitizaciju.
- Stara dokumentacija može sadržati interne detalje čak i kada ne sadrži credential-e.

## Implementation Plan

1. Potvrditi da se pod „zavisnosti ostaju kako jesu“ podrazumeva očuvanje ponašanja dva kontrolera, uz dozvoljeno uklanjanje nepovezanih metoda iz zajedničkog `CrmService`/`ICrmService`.
2. Zabeležiti baseline build/test rezultat pre source izmena.
3. Ukloniti osam nezadržanih kontrolera.
4. Ukloniti njihove BL implementacije i interfejse.
5. Svesti `ICrmService` i `CrmService` na dve potrebne metode, a zatim ukloniti ostale servise i servisne ugovore.
6. Ukloniti domenske business/data modele koji više nemaju reference.
7. Očistiti `DefaultProfile` i potvrditi AutoMapper konfiguraciju.
8. Očistiti Autofac registracije, named HTTP klijente, outbound auth komponente i konfiguracione ključeve koji više nisu potrebni.
9. Parcijalno očistiti mešovite test fajlove i ukloniti potpuno obsolete test fajlove.
10. Pokrenuti ciljane testove, zatim solution build i ceo test suite.
11. Uraditi odvojenu sanitizaciju source konfiguracije i dokumentacije.
12. Dodati `.gitignore`, ukloniti lokalne build/IDE artefakte i pokrenuti završni secret scan pre `git init`/prvog commita.
13. U drugom krugu generalizovati i preimenovati projekat i dva referentna primera.

## Validation Plan

- `dotnet build Template.Service.Api.sln`;
- `dotnet test Template.Service.Api.Tests/Template.Service.Api.Tests.csproj`;
- `dotnet test Framework.Logger.Tests/Framework.Logger.Tests.csproj`;
- AutoMapper configuration validation kroz `DefaultProfileTests`;
- DI resolution test za `ICrmBusinessLogic`, `IOrganizationBusinessLogic`, `ICrmService` i oba response handler-a;
- testovi kontrolera, Organization BL toka i dve zadržane `CrmService` metode;
- pretraga svih uklonjenih tipova i namespace-a bez compile-time referenci;
- pregled svih preostalih configuration key-eva i environment varijanti;
- secret scan svih source, test, configuration i documentation fajlova;
- potvrda da `.vs`, `bin`, `obj`, user-secrets i lokalni fajlovi nisu kandidati za prvi commit.

## Open Decision

Pre implementacije treba potvrditi sledeće tumačenje:

> `CrmController` i `OrganizationsController` ostaju funkcionalno nepromenjeni, ali zajednički `CrmService`, `ICrmService`, test fajlovi, mapper profil i DI konfiguracija smeju da se očiste od članova koji pripadaju obrisanim kontrolerima.

Bez ove dozvole kaskadno uklanjanje ostalih poslovnih modela nije moguće, jer njihove tipove i dalje izlažu potpisi `ICrmService` metoda.

## Update - 2026-09-09

Korisnik je potvrdio preporučeno tumačenje: dozvoljeno je ukloniti nepovezane metode iz `CrmService` i `ICrmService`, uz očuvanje ponašanja, ruta i ugovora `CrmController` i `OrganizationsController` tokova.

Prvi source cleanup krug je implementiran. Stara projektna dokumentacija, fizičko uklanjanje build/IDE artefakata, provera preklapanja root/health rute i generalno preimenovanje template-a ostaju odvojene stavke za sledeći krug.

Naknadnim pregledom potvrđeno je da se zadržava infrastrukturna dokumentacija. Dva interna Atlassian linka uklonjena su iz SR i EN Application Insights analiza; ostali označeni URL-ovi pripadaju javnom demo API-ju i nisu credential ili interni resurs.

