# Keycloak JWT serverska zaštita Service.Api.Template

## Summary

Svi postojeći i budući `Service.Api.Template` controller endpoint-i sada podrazumevano zahtevaju validan Keycloak JWT access token za audience `service-api-template`.

## Implementirane izmene

- Dodat je JWT Bearer handler za .NET 10.
- Validiraju se issuer, audience, lifetime i signing key.
- Uvedena je fail-fast validacija Keycloak konfiguracije.
- Globalna fallback authorization politika zahteva autentifikovanog korisnika.
- `UseAuthentication()` se izvršava pre `UseAuthorization()`.
- Development Swagger podržava Bearer token unos.
- Authentication logovi ne sadrže token, Authorization header ni claim vrednosti.

## Tok zahteva

```text
HTTP zahtev
  -> JWT Bearer autentifikacija
  -> globalna RequireAuthenticatedUser politika
  -> controller/action
```

Odsutan ili nevalidan token vraća `401`. Buduća role/policy zabrana za validno autentifikovanog korisnika vraćaće `403`.

## Konfiguracija

Svako okruženje mora podesiti `Authentication:Keycloak` vrednosti: HTTPS `Authority`, `Audience`, `RoleClientId` i `ClockSkewSeconds`. Audience je trenutno `service-api-template`, a početni clock skew 60 sekundi.

## Validacija

- Build: passed, 0 grešaka.
- Testovi: 40/40 passed.
- Validan lokalni test token: HTTP 200 na probe controller-u.
- Bez tokena, pogrešan issuer/audience/potpis ili istekao token: HTTP 401.
- Runtime: Swagger HTTP 200; `POST /Product` bez tokena HTTP 401.

## Otvoreno

- Stvarni Authority po okruženju i realna Keycloak E2E provera.
- Audience mapper konfiguracija caller client-a.
- Buduće role/policy odluke i eksplicitno odobreni anonimni health endpoint, ako bude potreban.

