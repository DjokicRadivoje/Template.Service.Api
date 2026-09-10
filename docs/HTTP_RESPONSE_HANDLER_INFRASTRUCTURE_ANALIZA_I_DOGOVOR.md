# HttpResponseHandler - analiza sloja i radni dogovor

## Status dokumenta

- Status: Implementirano i verifikovano
- Datum otvaranja: 2026-08-18
- Implementacija: Završena 2026-08-18
- Jira tiket: Nije naveden

## Kontekst

Klasa `HttpResponseHandler` trenutno se nalazi u:

```text
Template.Service.Services/Http/HttpResponseHandler.cs
namespace Template.Service.Services.Http
```

Projekat `Template.Service.Services` prvenstveno sadrži implementacije servisa koji predstavljaju konkretne poslovne ili integracione operacije, na primer `ProductService`. `HttpResponseHandler` nema znanje o proizvodima niti o konkretnom poslovnom zadatku. Njegova odgovornost je obrada HTTP transporta, deserijalizacije i tehničkih grešaka.

Cilj analize je da se utvrdi:

- kom arhitektonskom sloju `HttpResponseHandler` pripada;
- da li je dovoljna promena namespace-a ili je potreban zaseban projekat;
- gde treba da ostanu njegov interfejs i message codes;
- kako da se izmena izvrši bez nepotrebno širokog refaktora.

## Trenutne odgovornosti klase

`HttpResponseHandler` trenutno:

- izvršava prosleđenu HTTP akciju;
- raspolaže životnim ciklusom `HttpResponseMessage` objekta;
- proverava HTTP status;
- čita response stream;
- deserijalizuje JSON;
- razlikuje caller cancellation od timeout-a;
- obrađuje `HttpRequestException`;
- loguje tehničke detalje;
- mapira rezultat u standardni `Response<T>` i `Message` model.

Ovo su pretežno infrastrukturne odgovornosti. Klasa ne donosi poslovnu odluku o konačnom ishodu operacije i ne orkestrira konkretne domenske servise.

## Trenutni raspored komponenti

```text
Template.Service.Services
├── ProductService.cs
└── Http
    └── HttpResponseHandler.cs

Template.Service.Services.Interfaces
├── IProductService.cs
└── Http
    ├── IHttpResponseHandler.cs
    └── HttpResponseMessageCodes.cs
```

Aktuelni dependency tok relevantan za ovu temu je približno:

```text
Template.Service.BusinessLogic
    ↓
Template.Service.Services
    ↓
Template.Service.Services.Interfaces
    ↓
Template.Service.BusinessModel / Template.Service.DataModel
```

`Template.Service.DependencyInjection` trenutno registruje `ProductService` i `HttpResponseHandler`, ali do njihovih assembly-ja dolazi kroz tranzitivne project reference. To funkcioniše, ali čini stvarne compile-time zavisnosti DI projekta manje eksplicitnim.

## Zašto trenutni namespace nije idealan

Namespace `Template.Service.Services.Http` sugeriše da je handler jedna od implementacija poslovnih/integracionih servisa. U stvarnosti je on zajednički tehnički mehanizam koji koriste takvi servisi.

Posledice trenutnog rasporeda:

- mešaju se poslovne service implementacije i transportna infrastruktura;
- projekat `Template.Service.Services` dobija više od jedne arhitektonske odgovornosti;
- buduće generičke HTTP komponente bi se prirodno dodavale u pogrešan sloj;
- kolege koje koriste template mogu kopirati nejasnu granicu odgovornosti u nove API-je.

## Razmatrane opcije

### Opcija A - zadržati postojeće stanje

```text
Template.Service.Services.Http.HttpResponseHandler
```

Prednosti:

- nema migracije ni izmena project reference-a;
- trenutno rešenje funkcioniše.

Nedostaci:

- infrastrukturna komponenta ostaje u projektu namenjenom servisnim implementacijama;
- arhitektonska granica ostaje nejasna;
- problem se povećava dodavanjem novih HTTP helpera, policies ili serializer komponenti.

Ocena: Nije preporučeno za template koji treba da usmerava buduće servise.

### Opcija B - promeniti samo folder i namespace unutar istog projekta

Primer:

```text
Template.Service.Services/Infrastructure/Http/HttpResponseHandler.cs
namespace Template.Service.Services.Infrastructure.Http
```

Prednosti:

- mala i bezbedna izmena;
- namespace jasnije opisuje odgovornost.

Nedostaci:

- assembly i dalje meša servisni i infrastrukturni sloj;
- promena je prvenstveno organizaciona i ne uvodi stvarnu compile-time granicu.

Ocena: Izabrano kao svesno privremeni korak za trenutnu fazu implementacije.

### Opcija C - novi Template.Service.Infrastructure projekat

Predložena struktura:

```text
Template.Service.Infrastructure
└── Http
    └── HttpResponseHandler.cs

namespace Template.Service.Infrastructure.Http
```

Prednosti:

- jasno odvaja poslovne servise od transportne infrastrukture;
- uvodi stvarnu assembly/compile-time granicu;
- odgovara postojećem `Infrastructure` solution folderu;
- daje prirodno mesto za buduće HTTP policies, serializer options i slične tehničke komponente;
- zadržava `Template.Service.Services` fokusiranim na implementacije konkretnih servisnih operacija.

Nedostaci:

- dodaje novi projekat i project reference-e;
- DI registracija mora eksplicitno da referencira novi assembly;
- potrebno je odlučiti gde dugoročno pripadaju interfejs i message codes.

Ocena: Odloženo za kasniju fazu zbog većeg scope-a.

### Opcija D - premestiti u zajednički Framework projekat

Mogući naziv:

```text
Framework.Http
```

Ova opcija trenutno nije preporučena zato što `HttpResponseHandler` zavisi od aplikacionih tipova:

- `Response<T>`;
- `Message`;
- `MessageType`;
- `HttpResponseMessageCodes`.

Takva zavisnost bi učinila framework sloj zavisnim od ugovora konkretnog Web Service template-a. Za pravi reusable framework handler prvo bi trebalo definisati generički transportni rezultat nezavisan od `Template.Service.BusinessModel`, a zatim rezultat mapirati u `Response<T>` na višem sloju.

Ocena: Mogući budući pravac, ali preširok za trenutni cilj.

## Dugoročna preporuka - odloženo

Kreirati novi projekat:

```text
Template.Service.Infrastructure/Template.Service.Infrastructure.csproj
```

i premestiti implementaciju u:

```text
Template.Service.Infrastructure/Http/HttpResponseHandler.cs
namespace Template.Service.Infrastructure.Http
```

Ovo ostaje mogući budući pravac koji uvodi stvarnu arhitektonsku granicu. U trenutnoj fazi nije izabran zbog vremena i šireg scope-a. Samo menjanje namespace-a unutar `Template.Service.Services` projekta ne rešava potpuno problem odgovornosti assembly-ja, ali jasno označava infrastrukturnu ulogu klase bez uticaja na ponašanje.

## Usaglašeni scope trenutne implementacije

Za trenutnu fazu izabrana je opcija B:

```text
Template.Service.Services/Infrastructure/Http/HttpResponseHandler.cs
namespace Template.Service.Services.Infrastructure.Http
```

Implementirani scope:

1. Fajl je premešten iz `Template.Service.Services/Http` u `Template.Service.Services/Infrastructure/Http`.
2. Namespace je promenjen iz `Template.Service.Services.Http` u `Template.Service.Services.Infrastructure.Http`.
3. `DependencyInjectionConfig` koristi novi namespace.
4. `IHttpResponseHandler` i `HttpResponseMessageCodes` premešteni su u `Template.Service.Services.Interfaces/Infrastructure/Http` i koriste isti semantički namespace kao implementacija.
5. Nisu dodavani novi behavioral testovi jer izvršna logika nije menjana.
6. Izvršeni su solution build i postojeći testovi.
7. Project reference-i i izvršno ponašanje nisu menjani.

## Odloženi scope pune project/package migracije

Za prvi korak predlaže se:

1. Kreirati `Template.Service.Infrastructure` projekat.
2. Dodati ga u postojeći `Infrastructure` solution folder.
3. Premestiti samo implementaciju `HttpResponseHandler` u novi projekat i namespace.
4. Privremeno zadržati `IHttpResponseHandler` i `HttpResponseMessageCodes` u `Template.Service.Services.Interfaces` projektu.
5. Dodati eksplicitne project reference-e iz `Template.Service.DependencyInjection` ka projektima čije konkretne klase registruje.
6. Ažurirati Autofac registraciju da koristi `Template.Service.Infrastructure.Http.HttpResponseHandler`.
7. Dodati ciljane testove za handler u odgovarajući test projekat.
8. Potvrditi da `ProductService` i dalje zavisi samo od `IHttpResponseHandler`, a ne od konkretne infrastructure klase.

Ovaj scope ne menja ponašanje HTTP obrade; menja samo vlasništvo, assembly i namespace implementacije.

## Položaj interfejsa

`IHttpResponseHandler` je organizaciono premešten u:

```text
Template.Service.Services.Interfaces/Infrastructure/Http/IHttpResponseHandler.cs
namespace Template.Service.Services.Infrastructure.Http
```

Postoje tri moguća pravca:

1. Ostaviti ga privremeno u `Template.Service.Services.Interfaces` projektu.
2. Premestiti ga u novi `Template.Service.Infrastructure` projekat zajedno sa implementacijom.
3. Uvesti poseban `Template.Service.Infrastructure.Abstractions` projekat.

Za trenutni scope interfejs ostaje u postojećem `Template.Service.Services.Interfaces` assembly-ju, ali deli namespace sa implementacijom:

- `ProductService` već zavisi od interfaces projekta;
- implementacija može da referencira isti interfaces projekat;
- izbegava se dodatni abstractions projekat zbog jednog interfejsa;
- migration scope ostaje mali.

Ovim je uklonjen neodgovarajući `Services.Interfaces.Http` namespace bez promene assembly zavisnosti.

## Položaj HttpResponseMessageCodes

`HttpResponseMessageCodes` ne koristi samo handler. `ProductController` koristi `ServiceTimeout` kod prilikom mapiranja rezultata na HTTP 504.

Kodovi su premešteni samo organizaciono, u `Template.Service.Services.Interfaces/Infrastructure/Http`, i koriste `Template.Service.Services.Infrastructure.Http` namespace. Nisu premešteni u assembly konkretne implementacije, pa API/controller nije dobio novu project zavisnost.

Moguće dugoročne lokacije:

- `Template.Service.BusinessModel.Common`;
- poseban application/contracts namespace;
- postojeći interfaces projekat, dok se ne uradi šire sređivanje ugovora.

Njihovo eventualno buduće izdvajanje u contracts/common paket ostaje zasebna odluka.

## Predloženi dependency tok nakon prve migracije

```text
Template.Service.Services
    ├── koristi IHttpResponseHandler
    └── ne poznaje konkretnu implementaciju

Template.Service.Infrastructure
    └── implementira IHttpResponseHandler

Template.Service.DependencyInjection
    ├── referencira Template.Service.Services
    ├── referencira Template.Service.Infrastructure
    └── povezuje interfejse sa implementacijama
```

Novi infrastructure projekat treba da ima minimalne direktne reference, početno:

- `Template.Service.Services.Interfaces` zbog `IHttpResponseHandler` i message codes;
- `Template.Service.BusinessModel` ako je direktna referenca potrebna radi jasnog compile-time ugovora;
- logging abstractions ili odgovarajući shared framework reference.

## Testiranje

Migracija treba da sačuva postojeće ponašanje i pokrije najmanje:

- uspešan HTTP odgovor i deserijalizaciju;
- non-success HTTP status;
- prazan ili nevalidan JSON payload;
- `HttpRequestException`;
- timeout;
- caller cancellation koji se propagira;
- pravilno dispose-ovanje `HttpResponseMessage` objekta, ako se testira kroz odgovarajući stub;
- uspešnu Autofac registraciju i resolution kroz `IHttpResponseHandler`.

## Rizici

- Oslanjanje na tranzitivne project reference-e može prikriti nedostajuću direktnu zavisnost u `Template.Service.DependencyInjection` projektu.
- Istovremeno pomeranje implementacije, interfejsa i message codes može nepotrebno proširiti refaktor.
- Stavljanje trenutne klase u `Framework` uvodi obrnutu zavisnost framework-a prema aplikacionim modelima.
- Samo menjanje namespace-a bez novog projekta može ostaviti lažni utisak da je arhitektonska granica rešena.
- Testovi moraju potvrditi da migracija nije promenila mapiranje grešaka i cancellation semantiku.

## Kriterijumi prihvatanja trenutne migracije

- [x] Fajl se nalazi u `Template.Service.Services/Infrastructure/Http` folderu.
- [x] Implementacija koristi `Template.Service.Services.Infrastructure.Http` namespace.
- [x] `ProductService` i dalje zavisi samo od `IHttpResponseHandler`.
- [x] Autofac registracija koristi novi namespace.
- [x] Interfejs i message codes koriste isti infrastructure namespace kao implementacija.
- [x] Fizički su organizovani u `Template.Service.Services.Interfaces/Infrastructure/Http`, bez promene assembly-ja.
- [x] Project reference-i nisu menjani.
- [x] Postojeće HTTP ponašanje i response mapiranje ostaju nepromenjeni.
- [x] Solution build i postojeći testovi prolaze.

## Otvorene odluke za usaglašavanje

Nema otvorenih odluka koje blokiraju trenutnu namespace migraciju. Novi Infrastructure/Framework projekat, NuGet priprema, reorganizacija interfejsa/message codes i posebni handler testovi ostaju teme za kasniju fazu.

## Zaključak

`HttpResponseHandler` je infrastrukturna komponenta. U trenutnoj fazi ostaje u `Template.Service.Services` assembly-ju, ali je premešten u `Infrastructure/Http` folder i `Template.Service.Services.Infrastructure.Http` namespace kako bi njegova uloga bila jasna.

Izdvajanje u poseban project/NuGet paket ostaje dugoročna mogućnost. Trenutna klasa još nije dovoljno nezavisna od aplikacionih modela da bi bez dodatnog refaktora pripadala zajedničkom `Framework` sloju.

## Dopuna - eksplicitne project zavisnosti (2026-08-18)

Naknadno je uklonjeno oslanjanje na tranzitivne project reference-e:

- `Template.Service.BusinessLogic` direktno referencira `Template.Service.Services.Interfaces`, a više ne referencira konkretnu implementaciju `Template.Service.Services`;
- `Template.Service.DependencyInjection` eksplicitno referencira `Template.Service.BusinessLogic`, `Template.Service.BusinessLogic.Interfaces`, `Template.Service.Services.Interfaces` i `Template.Service.Services`, čije tipove koristi pri Autofac registraciji;
- `Template.Service.Api` više nema direktne reference na konkretne `Template.Service.BusinessLogic` i `Template.Service.Services` projekte, jer implementacije dobija kroz composition root.

Ovim je compile-time dependency smer usklađen sa korišćenim apstrakcijama bez promene izvršnog ponašanja. Solution build je uspešan uz dva ranije postojeća nullable upozorenja, a svi testovi prolaze (21/21).

## Validacija

- `dotnet build Template.Service.Api.sln -c Debug --no-restore` - uspešno, 0 grešaka i 0 upozorenja u finalnom prolazu.
- `dotnet test Template.Service.Api.sln -c Debug --no-build --no-restore` - uspešno, 12/12 testova.
- Pretraga source referenci potvrđuje da se stari `Template.Service.Services.Http` i `Template.Service.Services.Interfaces.Http` namespace-i više ne koriste.

## Granica implementiranog koraka

Promenjene su fizičke lokacije i namespace-i implementacije, interfejsa i message codes, kao i odgovarajući `using` izrazi. Njihov javni API, izvršna logika, project reference-i i solution struktura nisu menjani.


