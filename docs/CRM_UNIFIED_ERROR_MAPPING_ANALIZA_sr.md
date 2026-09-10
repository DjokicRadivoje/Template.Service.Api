# Zajedničko CRM error mapiranje - analiza

## Izvor i scope

- Izvor ugovora: `docs/CRM.api/swagger20260902.json`, verzija od 2. septembra 2026.
- Obuhvaćene CRM metode: RSM-130, RSM-133, RSM-147, RSM-150 i RSM-155, kao i buduće `/Api/V8/custom/*` metode koje koriste isti error ugovor.
- Grana: `development`.
- Datum analize: 2. septembar 2026.
- Ovaj dokument definiše zajednički arhitekturni pravac. Implementacija se uvodi postepeno kroz pojedinačne tikete, počevši od RSM-149/RSM-150 mapiranja.

## Cilj

Servisni sloj treba da sadrži što manje poslovne logike. Njegova odgovornost je:

- slanje HTTP zahteva;
- deserializacija success ili error payload-a;
- čuvanje HTTP/transportnog outcome-a;
- tehničko logovanje bez izlaganja osetljivih ili internih detalja.

BusinessLogic sloj treba da bude vlasnik:

- mapiranja CRM greške u javni Service API Template status i message code;
- endpoint-specifičnih poslovnih odluka;
- bezbednog teksta koji se vraća klijentu;
- odluke da li određeni CRM `detail` menja poslovni rezultat.

## Swagger nalazi

Sve trenutno dokumentovane `/Api/V8/custom/*` metode koriste zajednički `#/components/schemas/Error` ugovor:

```json
{
  "code": "VALIDATION_ERROR | NOT_FOUND | BUSINESS_RULE | UNAUTHORIZED | FORBIDDEN | INTERNAL_ERROR",
  "message": "CRM error message",
  "correlationId": "CRM body value",
  "detail": "endpoint-specific-domain-code"
}
```

`Correlation-ID` se po dogovoru koristi isključivo kao HTTP header. Vrednost `correlationId` iz CRM error body-ja se ne prenosi u Service API Template body i nije deo poslovnog mapiranja.

Metode se razlikuju po HTTP statusima i `detail` vrednostima, ali ne po strukturi error objekta:

| CRM metoda | Statusi | Primeri `detail` vrednosti |
|---|---|---|
| RSM-130 subscription upsert | `400`, `401`, `403`, `422`, `500` | `product_not_found`, `customer_not_found`, `invalid_status` |
| RSM-150 customer upsert | `400`, `401`, `403`, `422`, `500` | `missing_last_name`, `invalid_source_occurred_at` |
| RSM-155 KPU registration | `400`, `401`, `403`, `404`, `422`, `500` | `kpu_product_not_found`, `kpu_customer_not_found`, `invalid_registered_at` |
| RSM-147 account search | `401`, `403`, `422`, `500` | `invalid_page`, `invalid_page_size` |
| RSM-133 subscription transition | `400`, `401`, `403`, `404`, `422`, `500` | `invalid_action`, `invalid_state_transition`, `override_info_required` |

Swagger na pojedinim `401`, `403` i `500` response definicijama ne ponavlja `$ref` ka `Error` schemi, ali glavni Swagger opis potvrđuje da sve `/Api/V8/custom/*` metode na grešci vraćaju unified Error objekat.

## Arhitekturna odluka

Koristi se jedan zajednički CRM error transportni model i jedan zajednički HTTP response reader. Ne uvode se posebni transportni error modeli ili HTTP handleri po metodi.

Zajedničko mapiranje canonical CRM grešaka u bezbedne Service API Template greške nalazi se u BusinessLogic sloju. Pojedinačna BusinessLogic metoda daje svoje javne message kodove i, samo kada je potrebno, endpoint-specifična pravila za CRM `detail`.

```text
CRM HTTP response
  -> zajednički CRM response reader
     -> CrmServiceResult<T> + CrmErrorResponse
        -> BusinessLogic zajednički error mapper/helper
           -> endpoint-specifični javni code/status/text
              -> Response<TBusiness>
```

## Predložene komponente

### CRM transportni error model

Predložena lokacija:

```text
Template.Service.DataModel/Crm/CrmErrorResponse.cs
```

Minimalna polja potrebna za interno mapiranje:

- `Code`;
- `Message`;
- `Detail`.

Body `CorrelationId` se ne koristi; correlation se vodi isključivo kroz postojeći `Correlation-ID` header pipeline.

### Rezultat CRM servisnog poziva

Predložena lokacija ugovora:

```text
Template.Service.Services.Interfaces/Infrastructure/Http/CrmServiceResult.cs
```

Rezultat treba da prenese činjenice, bez poslovne odluke:

- success DTO ili `null`;
- HTTP status;
- deserializovani `CrmErrorResponse` ili `null`;
- transportni failure tip, npr. timeout, unavailable ili deserialization failure;
- indikator uspeha izveden iz HTTP/transportnog rezultata.

`CrmServiceResult<T>` nije javni API response i ne serijalizuje se klijentu.

### Zajednički CRM response reader

Predložena lokacija:

```text
Template.Service.Services/Infrastructure/Http/CrmResponseHandler.cs
```

Odgovornosti:

- za `2xx` deserializuje proizvoljan success DTO;
- za non-`2xx` deserializuje zajednički `CrmErrorResponse`;
- čuva HTTP status i transportni failure;
- podržava i `ok/error` success envelope gde ga CRM metoda stvarno koristi;
- ne bira `CUSTOMER_DATA_INVALID`, `SUBSCRIPTION_NOT_FOUND` ili druge Service API Template kodove;
- ne kopira sirovi CRM `message` ili `detail` u javni `Response.Messages`.

Postojeći generic constraint `where T : CrmApiResponse` treba ukloniti ili zameniti opcionim marker interfejsom za metode čiji success DTO zaista ima `ok/error` envelope. Flat success DTO-i RSM-147, RSM-150 i RSM-155 ne smeju zbog odsutnog `ok` polja biti označeni kao neuspešni.

### BusinessLogic error mapper

Predložena lokacija:

```text
Template.Service.BusinessLogic/Infrastructure/CrmErrorMapper.cs
```

Alternativno, postojeći `BusinessLogicResponseHelper` može biti proširen ako rezultat ostane dovoljno čitljiv i testabilan.

Odgovornosti zajedničkog BusinessLogic mappera:

- mapiranje HTTP statusa i canonical CRM `code` u `ResponseStatus`;
- izbor bezbednog javnog teksta;
- dodavanje javnog, endpoint-specifičnog message code-a;
- zabrana prosleđivanja sirovog CRM `message`, `detail` i body correlation ID-a;
- podrška za opcionu endpoint policy mapu samo kada `detail` nosi poslovno relevantnu razliku.

Primer poziva iz customer BusinessLogic-a konceptualno treba da izgleda ovako:

```text
CrmErrorMapper.Apply(
    serviceResult,
    businessResponse,
    validationCode: CUSTOMER_DATA_INVALID,
    failureCode: CUSTOMER_UPSERT_FAILED)
```

## Zajedničko naspram endpoint-specifičnog mapiranja

| Odgovornost | Zajednička | Po metodi |
|---|---:|---:|
| Deserializacija unified Error JSON-a | Da | Ne |
| Obrada timeout/unavailable/deserialization failure-a | Da | Ne |
| Prepoznavanje canonical CRM `code` | Da | Ne |
| Sprečavanje curenja CRM `message/detail` | Da | Ne |
| Izbor javnog Service API Template message code-a | Ne | Da, kroz parametre/policy u BusinessLogic-u |
| Mapiranje posebnog `detail` koda koji menja poslovni ishod | Ne | Da, samo kada postoji konkretna potreba |
| Pisanje posebnog error handlera za svaki endpoint | Ne | Ne |

Za RSM-149 nisu potrebna posebna `detail` pravila. `400` i CRM validation outcome mapiraju se na javni `CUSTOMER_DATA_INVALID`, dok ostali integration failure-i koriste `CUSTOMER_UPSERT_FAILED`. Duplicate i company ambiguity stanja dolaze kao uspešan HTTP `200` sa statusima i warnings, pa ne pripadaju error mapperu.

## Uticaj na postojeći kod

- `ICrmService` metode trenutno vraćaju `BusinessModel.Common.Response<T>`, zbog čega se transportni i poslovni rezultat mešaju.
- `BusinessLogicResponseHelper.ApplyServiceFailure` trenutno kopira sve service poruke u javni response.
- `HttpResponseHandler` na non-success odgovoru odbacuje CRM error body.
- `CrmResponseHandler` očekuje `CrmApiResponse` sa `ok/error` envelope-om i ne podržava sve flat success DTO-e iz Swaggera.

Preporučeni pravac je da realne CRM metode postepeno pređu na `CrmServiceResult<T>`. Migracija može početi sa RSM-149/RSM-150 i zatim se primeniti na ostale metode kada njihovo stvarno CRM mapiranje dođe na red.

## Implementacioni redosled

- [x] Dodati zajednički `CrmErrorResponse` transportni model.
- [x] Definisati `CrmServiceResult<T>` bez zavisnosti od javnog Service API Template response ugovora.
- [x] Generalizovati CRM response handler za flat success, opcioni `ok/error` envelope i unified error response.
- [x] Dodati zajednički BusinessLogic `CrmErrorMapper`.
- [x] Prvo primeniti novi tok na RSM-149/RSM-150.
- [ ] Posle validacije migrirati RSM-130, RSM-133, RSM-147 i RSM-155 kada se njihove servisne integracije implementiraju ili ažuriraju.
- [ ] Ukloniti stare/duplirane handler tokove tek kada više nemaju korisnike.

## Plan testova

- [x] Zajednički handler: canonical CRM error kodovi se deserializuju.
- [x] Zajednički handler: `400`, `401`, `403`, `404`, `422` i `500` čuvaju HTTP status.
- [x] Zajednički handler: flat success response se ne tretira kao neuspešan zbog odsutnog `ok` polja.
- [x] Zajednički handler: postojeći `ok/error` envelope testovi nastavljaju da rade.
- [x] BusinessLogic mapper: CRM interni `message`, `detail` i body correlation ID ne dospevaju u javni response.
- [x] BusinessLogic mapper: RSM-149 dobija svoj javni validation/failure code.
- [x] RSM-149 flow testovi: validation i integration failure mapiranje.
- [x] Regresioni testovi za već aktivnu RSM-130 servisnu integraciju kroz puni solution suite.

## Rizici i zaštitne mere

| Rizik | Zaštitna mera |
|---|---|
| Promena `ICrmService` return tipa utiče na više BusinessLogic klasa i test stubova. | Migrirati metodu po metodu i privremeno zadržati stari adapter gde je potreban. |
| Sirovi CRM error detalji mogu procuriti kroz `Response.Messages`. | Nikada ih ne kopirati direktno; javne poruke formira isključivo BusinessLogic mapper. |
| RSM-130/RSM-133 koriste `ok/error`, dok druge metode imaju flat success. | Opcioni marker/interfejs proveravati samo za DTO-e koji ga implementiraju. |
| `detail` kodovi mogu rasti bez promene Swagger Error schemata. | Nepoznati `detail` mapirati na bezbedni endpoint failure code i logovati strukturirano. |
| Široka migracija bi povećala rizik tiketa RSM-149. | U RSM-149 implementirati zajedničku osnovu i samo customer vertikalu; ostale metode migrirati odvojeno. |

## Status

- Arhitekturni pravac je dogovoren.
- Zajednička osnova implementirana je 2. septembra 2026. i primenjena na RSM-149/RSM-150.
- Ostale CRM metode još koriste postojeće tokove i migriraju se kroz svoje tikete.


