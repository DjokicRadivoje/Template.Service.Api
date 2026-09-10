# Zaključci: Response model i obrada HTTP poziva

## Kontekst i cilj

Ovaj API predstavlja šablon koji će kolege koristiti kao početni kostur za druge domenske API-je. Primeri zato treba da budu tehnički ispravni, jednostavni za kopiranje i dovoljno jasni da usmere dalji razvoj bez uvođenja nepotrebnog framework-a.

Dogovorena podela odgovornosti je:

```text
Controller
    prima request i prosleđuje ga BusinessLogic sloju
        ↓
BusinessLogic
    mapira business request na jedan ili više DataModel objekata
    orkestrira pozive jednog ili više servisa i lokalnu poslovnu logiku
    formira konačni rezultat operacije
        ↓
Service
    izvršava konkretan poziv ka eksternom ili internom sistemu
        ↓
HttpResponseHandler
    centralizovano obrađuje HTTP transport i deserijalizaciju
```

## Response model

`Response<T>` je standardni omotač rezultata operacije. `Messages` treba da sadrži mapirane i bezbedne poruke nastale tokom cele orkestracije:

- error poruke;
- warning poruke;
- info poruke;
- debug poruke kada su dozvoljene okruženjem i pravilima izlaganja.

Poruke mogu poticati iz:

- eksternog API-ja;
- internog domenskog API-ja;
- baze podataka;
- mapiranja;
- servisne ili poslovne obrade.

Za HTTP pozive, `HttpResponseHandler` mapira rezultat poziva na zajednički `Response<T>` format i formira standardne `Message` objekte za HTTP status, timeout, transportnu grešku i grešku deserijalizacije. Servisni sloj definiše konkretan HTTP poziv i očekivani tip odgovora.

Ako eksterni API ima sopstveni specifičan error ugovor, servisni sloj može handleru proslediti funkciju koja taj poznati error model mapira u standardni `Message` format. Kreiranje konačnog `Response<T>` omotača i dalje ostaje odgovornost handlera.

Sirovi exception detalji, stack trace, connection string, poverljivi podaci, interni URL-ovi i kompletan neproveren response body ne smeju biti prosleđeni klijentu. Tehnički detalji ostaju u server-side logu.

Ovo razgraničenje odnosi se na HTTP pozive. Rezultati baze podataka i drugih transporta nisu odgovornost `HttpResponseHandler` klase i obrađuju se u komponenti koja poznaje taj konkretan način pristupa podacima.

BusinessLogic treba da doda i svoju konačnu poruku kada operacija nije uspela. Ta poruka predstavlja značenje greške iz ugla trenutnog domenskog API-ja, dok `Messages` zadržava istoriju događaja koji su doveli do ishoda.

Tačan naziv i konačna struktura posebne završne poruke (`FinalMessage`, `Outcome` ili slično) nisu deo trenutne implementacije i mogu biti definisani kada se nastavi razvoj modela.

## BusinessLogic obrazac

Svaka BL metoda treba da instancira svoj `Response<T>` na početku metode. Mapiranja, servisni pozivi i poslovna obrada izvršavaju se unutar `try` bloka.

Servisne poruke se akumuliraju:

```csharp
response.Messages.AddRange(serviceResponse.Messages);
```

Lista se ne zamenjuje dodelom, jer jedna BL metoda može pozvati više servisa i treba da zadrži poruke svih prethodnih koraka.

Neočekivani exception:

- loguje se server-side zajedno sa tehničkim detaljima;
- ne izlaže exception detalje klijentu;
- dodaje bezbednu, stabilnu BL poruku u response.

Caller cancellation se ne pretvara u poslovnu grešku. `OperationCanceledException`, kada je otkazan prosleđeni `CancellationToken`, treba propagirati dalje.

Za orkestracije sa više poziva potrebno je na nivou konkretne BL metode odlučiti:

- da li se obrada prekida posle prve greške;
- da li su pozivi međusobno nezavisni;
- da li je dozvoljen fallback;
- da li je dozvoljen parcijalni rezultat.

## Eksterni HTTP pozivi

Postojeći `HandleAsync<T>` namenjen je sistemima koji vraćaju običan JSON payload, bez standardnog `Response<T>` omotača.

Primer ulaza:

```json
[
  { "id": 1, "name": "Product" }
]
```

Handler tada:

1. izvršava HTTP poziv;
2. proverava HTTP rezultat;
3. deserijalizuje uspešan payload u `T`;
4. formira `Response<T>`;
5. mapira timeout, transportnu grešku i grešku deserijalizacije u standardne poruke;
6. loguje tehničke exception detalje samo server-side.

Ako konkretan eksterni API vraća strukturisani error model, handler može imati overload koji prima funkciju za njegovo mapiranje, približno:

```csharp
Task<Response<TData>> HandleAsync<TData, TError>(
    Func<CancellationToken, Task<HttpResponseMessage>> action,
    Func<TError, Message> errorMapper,
    CancellationToken cancellationToken = default);
```

Servis time pruža samo znanje o ugovoru konkretnog API-ja. Handler i dalje izvršava HTTP obradu i formira `Response<TData>`.

## Interni HTTP pozivi

Za pozive ka internim domenskim API-jima dogovoreno je dodavanje posebne metode u `HttpResponseHandler`, predloženog naziva:

```csharp
HandleInternalAsync<T>
```

Interni API već vraća standardni oblik:

```csharp
Response<T>
```

Zato ova metoda ne sme da napravi:

```csharp
Response<Response<T>>
```

Njen zadatak je da deserijalizuje telo direktno kao `Response<T>` i vrati dobijeni standardni response sa njegovim podacima i porukama.

Predloženi potpis je:

```csharp
Task<Response<T>> HandleInternalAsync<T>(
    Func<CancellationToken, Task<HttpResponseMessage>> action,
    CancellationToken cancellationToken = default);
```

Ako interni API vrati validan `Response<T>`, njegov sadržaj treba sačuvati i kada je HTTP status non-2xx. HTTP status internog API-ja ne treba automatski preslikavati u status našeg kontrolera niti njime pregaziti standardni response koji je interni API vratio.

Ako validan interni response nije dostupan zbog timeout-a, transportne greške, praznog sadržaja ili neispravnog JSON-a, handler formira lokalni `Response<T>` sa odgovarajućom bezbednom infrastrukturnom porukom.

Primer korišćenja u servisnom sloju:

```csharp
return _httpResponseHandler.HandleInternalAsync<List<ProductResponse>>(
    token => _httpClient.GetAsync("internal-api/products", token),
    cancellationToken);
```

## Odnos servisnih i BL poruka

`HttpResponseHandler` formira `Response<T>` i standardne poruke za rezultat HTTP poziva. Servisni sloj definiše konkretan poziv i eventualno pruža mapiranje specifičnog error ugovora. BusinessLogic koristi dobijene poruke kao istoriju orkestracije, ali sam određuje konačno značenje rezultata za svoj domen.

Primer:

```text
Interni API vrati svoju servisnu grešku
    ↓
HttpResponseHandler deserijalizuje i vraća standardni Response
    ↓
BusinessLogic je dodaje u Messages
    ↓
BusinessLogic dodaje svoju konačnu domensku poruku
```

Controller ne treba da određuje svoj rezultat direktno na osnovu pojedinačnog statusa ili koda eksternog/internog sistema. Krajnji odgovor našeg API-ja treba da predstavlja odluku njegovog BusinessLogic sloja.

## Centralizovano mapiranje Response rezultata na HTTP status

Controller akcije koje vraćaju standardni `Response<T>` ne sadrže logiku za izbor HTTP statusa. Akcija poziva BusinessLogic i vraća rezultat kao `Ok`:

```csharp
return Ok(await _productBusinessLogic.GetProduct(request, cancellationToken));
```

Globalni `ResponseHttpStatusFilter` pre slanja odgovora prepoznaje svaki objekat koji implementira negenerički `IResponse` ugovor i centralno primenjuje status pravila:

- uspešan response ostaje HTTP 200;
- `BUSINESS_LOGIC_ERROR` se mapira na HTTP 500;
- `SERVICE_TIMEOUT` se mapira na HTTP 504;
- ostale standardne error poruke se mapiraju na HTTP 502.

Business error ima prioritet nad timeout kodom, čime je sačuvano prethodno ponašanje controllera. Filter menja samo standardni response čiji je inicijalni status 200. Eksplicitni non-200 rezultat, file/redirect rezultat i nestandardni DTO ostaju netaknuti.

Automatski `[ApiController]` model-validation odgovor 400 nastaje pre izvršavanja akcije i nije deo ovog filtera. Standardizacija validation odgovora je zasebna tema.

## Važna otvorena tačka

Ako `Messages` predstavlja kompletnu istoriju orkestracije, moguće je da sadrži `Error` iz neuspešnog prvog pokušaja, a da fallback kasnije uspe. U tom slučaju konačni `Success` ne može pouzdano da se računa samo proverom da li bilo koja istorijska poruka ima tip `Error`.

Konačno pravilo za `Success`, završnu poruku i eventualni status operacije nije deo ovog ograničenog koraka. Potrebno ga je definisati pre uvođenja fallback ili partial-success scenarija.

## Granica trenutnog dogovora

Trenutni zaključak je ograničen na:

- `Response<T>` kao zajednički oblik rezultata;
- formiranje `Response<T>` i standardnih HTTP poruka u `HttpResponseHandler` klasi;
- servisni sloj koji definiše konkretan poziv i samo po potrebi pruža mapiranje specifičnog error ugovora;
- akumuliranje bezbedno mapiranih poruka u BL sloju;
- BL kao vlasnika konačne domenske poruke;
- `HandleAsync<T>` za obične eksterne payload-e;
- budući `HandleInternalAsync<T>` za interne API-je koji već vraćaju `Response<T>`;
- razdvajanje caller cancellation-a od stvarnog HTTP timeout-a.

U ovom koraku nisu dogovoreni niti implementirani širi shared package, novi framework, base klase ili dodatne arhitektonske apstrakcije.
