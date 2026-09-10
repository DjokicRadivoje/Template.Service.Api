# Keycloak JWT serverska zaštita Service.Api.Template - analiza

## Status

- Faza: implementirano i lokalno validirano; realna Keycloak E2E provera je pending.
- Datum analize: 20. avgust 2026.
- Grana tokom analize: `main`.
- Preduslov: .NET 10 migracija završena i lokalno validirana 20. avgusta 2026.
- Redosled: serverska zaštita se implementira pre javnog CRM mock endpointa.
- Referentni dokument: `E:\GIT_2026\rafajlovski-chatbot_2026\keycloakJWT.md`.

## Cilj

Zaštititi sve postojeće i buduće `Service.Api.Template` controller akcije validacijom dolaznog Keycloak JWT access tokena. Podrazumevano nijedna controller akcija ne sme biti anonimna.

Početni authorization zahtev je autentifikovan pozivalac sa validnim tokenom za audience `service-api-template`. Role-based ograničenja nisu potrebna za postojeći `ProductController`, ali infrastruktura treba da omogući kasnije precizne policy provere.

## Implementirano stanje

- Dodat je `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.11.
- `AddKeycloakAuthentication(...)` registruje podrazumevanu Bearer šemu i eksplicitnu validaciju issuer-a, audience-a, lifetime-a i signing key-a.
- `MapInboundClaims` je isključen, a clock skew je konfigurabilan i ograničen na 0-300 sekundi.
- Konfiguracija fail-fast proverava obaveznu sekciju, apsolutni HTTPS Authority, Audience i clock skew.
- Globalna fallback politika zahteva autentifikovanog korisnika za svaki controller endpoint bez potrebe za pojedinačnim `[Authorize]` atributima.
- `UseAuthentication()` je dodat pre `UseAuthorization()`.
- Swagger u Development okruženju ima Bearer/JWT security definiciju.
- Bezbedni authentication događaji loguju samo putanju i tip greške, bez tokena, header-a ili claim vrednosti.
- Role parsing nije uveden jer nije deo početnog zahteva; `RoleClientId` ostaje rezervisana konfiguracija za kasnije precizne policy-je.

## Očekivano ponašanje

| Scenario | Rezultat |
|---|---|
| Nema Authorization header-a | `401 Unauthorized` |
| Token je nevalidno potpisan ili istekao | `401 Unauthorized` |
| Token ima pogrešan issuer | `401 Unauthorized` |
| Token nema `service-api-template` audience | `401 Unauthorized` |
| Token je validan za `service-api-template` | Controller akcija se izvršava |
| Endpoint kasnije zahteva rolu koju token nema | `403 Forbidden` |
| Novi controller nema `[Authorize]` | I dalje je zaštićen fallback politikom |
| Endpoint ima odobren `[AllowAnonymous]` | Dostupan bez tokena |

## Usvojeni trust model

- `Authority`: tačan HTTPS Keycloak realm URL po okruženju.
- `Audience`: `service-api-template`.
- `RoleClientId`: `service-api-template`, samo ako se mapiraju Keycloak client role.
- Prihvata se jedan dogovoreni issuer i jedan resource-server audience.
- Ne prihvataju se `account`, frontend audience ili role iz drugih Keycloak client sekcija bez dokumentovanog trust modela.
- Dolazni token je nezavisan od `client_credentials` tokena kojim `Service.Api.Template` poziva CRM.

Pozivaoci moraju dobiti access token čiji `aud` sadrži `service-api-template`. To zahteva odgovarajući Keycloak Audience/Audience Resolve mapper na klijentu koji izdaje pozivaočev token.

## Implementirano rešenje

### Options i validacija konfiguracije

Uveden je strongly typed inbound options model `KeycloakAuthenticationOptions` za:

- `Authority`
- `Audience`
- `RoleClientId`
- `ClockSkewSeconds`, preporučeno početno 60

Konfiguraciju validirati pri startup-u:

- Authority mora biti apsolutan HTTPS URI u realnim okruženjima;
- Audience je obavezan;
- RoleClientId je obavezan samo kada je role mapping uključen;
- clock skew ne sme biti negativan.

### JWT Bearer registracija

Na .NET 10 dodati framework-usaglašenu `Microsoft.AspNetCore.Authentication.JwtBearer` referencu i registrovati podrazumevanu Bearer šemu.

Token validation mora eksplicitno proveravati:

- issuer;
- audience;
- lifetime;
- signing key;
- dogovoreni clock skew.

Postaviti `MapInboundClaims = false` da originalna imena JWT claim-ova ostanu predvidiva.

### Globalna authorization fallback politika

Koristiti fallback policy:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

Ovo štiti postojeći `ProductController`, budući `CrmController` i sve nove controller akcije bez oslanjanja na ručno dodavanje `[Authorize]` atributa.

`[AllowAnonymous]` je dozvoljen samo za unapred dogovoren endpoint. Trenutno nije potvrđen nijedan anoniman poslovni endpoint.

### Middleware redosled

```text
UseCorrelationId
  -> UseHttpsRedirection
  -> UseAuthentication
  -> UseAuthorization
  -> MapControllers
```

`UseAuthentication()` mora biti pre `UseAuthorization()`.

### Role mapping

Početni scope zahteva samo validan token, pa custom role parser nije neophodan za osnovnu zaštitu. Ako se uvodi da podrži buduće role policy-je, sme da čita samo:

```text
resource_access.<RoleClientId>.roles
```

Parser mora proveriti JSON strukturu, izbeći duplikate i kontrolisano oboriti autentifikaciju kada je dogovoreni claim nevalidan. Ne sme sabirati role iz svih client sekcija.

### Swagger

Swagger ostaje samo u Development okruženju. Dodati Bearer security scheme da developer može uneti access token i pozvati zaštićene metode.

Fallback policy ne štiti automatski proizvoljan middleware kao što je Swagger UI. Ako Swagger ikada bude uključen van Development okruženja, potrebna je posebna zaštita ili mrežno ograničenje.

## Konfiguracija

Neosetljivi primer:

```json
{
  "Authentication": {
    "Keycloak": {
      "Authority": "https://<keycloak-host>/realms/<realm>",
      "Audience": "service-api-template",
      "RoleClientId": "service-api-template",
      "ClockSkewSeconds": 60
    }
  }
}
```

Authority i audience nisu secret-i, ali moraju biti environment-specific. Dokumentacija ne sadrži stvarne interne host vrednosti.

## Pogođeni fajlovi i oblasti

- `Template.Service.Api/Template.Service.Api.csproj` - JWT Bearer package na .NET 10.
- `Template.Service.Api/Program.cs` - registracija autentifikacije, fallback policy, middleware redosled i Swagger security scheme.
- novi options/extension fajlovi u `Template.Service.Api` infrastructure oblasti.
- `Template.Service.Api/appsettings*.json` - neosetljivi placeholder/config ključevi.
- `Template.Service.Api.Tests` - authorization i token validation testovi.
- svi postojeći i budući controller-i - ponašanje se menja globalno, iako ne zahtevaju pojedinačni atribut.

## Bezbednosna pravila

- Validirati potpis, issuer, audience i lifetime za svaki token.
- Ne logovati token ili Authorization header.
- Logovati samo bezbedne podatke kao što su status, putanja i correlation ID.
- Ne vraćati detalje token validation exception-a klijentu.
- `401` koristiti za odsutan/nevalidan token, `403` za validan token bez dozvole.
- Ne širiti accepted audience listu radi privremenog prolaska testa.
- Ne isključivati HTTPS metadata u realnim okruženjima.
- Eksplicitno testirati clock skew; podrazumevana bibliotečka vrednost ne treba da ostane neprimećena odluka.

## Rizici

- Pogrešan audience mapper u Keycloak-u može odbiti sve legitimne pozivaoce.
- Globalna zaštita može prekinuti monitoring ili integracije koje su se oslanjale na anoniman pristup.
- Swagger/health očekivanja mogu biti nejasna.
- Preširoko role mapiranje može prihvatiti istoimenu rolu drugog client-a.
- Startup validacija može namerno zaustaviti aplikaciju ako environment nema potrebnu konfiguraciju.

## Otvorena pitanja za environment/E2E fazu

- Stvarni `Authority` po okruženju.
- Koji Keycloak client-i izdaju tokene za stvarne pozivaoce i kako dobijaju `service-api-template` audience.
- Da li će biti uveden health endpoint koji mora biti eksplicitno `[AllowAnonymous]`.
- Koje role/policy provere se uvode nakon osnovne autentifikacije.

Za implementaciju je usvojen inbound audience `service-api-template`, Development-only Swagger i clock skew od 60 sekundi. Te vrednosti se mogu promeniti environment konfiguracijom bez izmene koda, osim audience trust modela koji mora ostati eksplicitna odluka.

Stvarne Keycloak vrednosti ne blokiraju implementaciju sa test konfiguracijom, ali blokiraju realnu E2E validaciju.

## Pristup validaciji

### Statička i DI validacija

- autentifikaciona šema i options se razrešavaju;
- aplikacija fail-fast reaguje na nedostajuću ili nevalidnu konfiguraciju;
- middleware redosled je pravilan;
- endpoint metadata sadrži fallback authorization zahtev.

### Integracioni API testovi

- poziv `ProductController` bez tokena vraća `401`;
- validan lokalno generisan test token za `service-api-template` prolazi;
- pogrešan issuer, audience, potpis ili expiry vraća `401`;
- novi test controller bez `[Authorize]` je zaštićen fallback politikom;
- eksplicitni `[AllowAnonymous]` test endpoint, ako je odobren, ostaje dostupan;
- role policy vraća `403` za validan token bez role.

Testovi ne treba da zavise od dostupnosti realnog Keycloak servera. Realni realm se koristi u odvojenoj environment integracionoj proveri.

### Ručna/E2E validacija

- dekodirati test token bez logovanja njegove vrednosti i potvrditi issuer/audience;
- pozvati API bez tokena, sa nevalidnim tokenom i sa validnim tokenom;
- potvrditi da logovi ne sadrže token;
- potvrditi Swagger Bearer unos u Development režimu.

## Rezultat validacije i acceptance kriterijumi

- [x] Svi controller endpoint-i podrazumevano zahtevaju autentifikaciju.
- [x] `ProductController` bez tokena vraća `401`.
- [x] Validan lokalno potpisan `service-api-template` test token prolazi.
- [x] Pogrešan issuer/audience/signature/lifetime vraća `401`.
- [x] `UseAuthentication()` je pre `UseAuthorization()`.
- [x] Swagger podržava Bearer unos u Development režimu.
- [x] Ne postoje nenamerno anonimni postojeći poslovni endpoint-i; fallback politika pokriva i buduće controller-e.
- [x] Testovi ne zahtevaju mrežni pristup realnom Keycloak-u.

Lokalna validacija 20. avgusta 2026:

- `dotnet build Template.Service.Api.sln --configuration Debug` - passed, 0 grešaka;
- `dotnet test Template.Service.Api.sln --configuration Debug --no-build` - passed, 40/40;
- Development runtime probe - Swagger `200`, `POST /Product` bez tokena `401`;
- pretraga runtime loga nije pronašla token, Authorization header ni test claim vrednost.

Realni Keycloak token nije testiran jer environment Authority i caller client konfiguracija još nisu dostavljeni.

## Preporučeni commit scope

```text
feat: require Keycloak JWT for Service API Template endpoints
```

Commit se pravi samo na eksplicitan zahtev korisnika.

