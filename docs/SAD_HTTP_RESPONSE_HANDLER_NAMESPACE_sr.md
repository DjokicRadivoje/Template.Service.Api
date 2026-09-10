# HttpResponseHandler namespace migracija

## Summary

`HttpResponseHandler`, njegov interfejs i message codes organizaciono su grupisani u zajednički infrastructure namespace unutar postojećih projekata.

## Implemented Changes

- Nova putanja: `Template.Service.Services/Infrastructure/Http/HttpResponseHandler.cs`.
- Interfejs i codes: `Template.Service.Services.Interfaces/Infrastructure/Http`.
- Zajednički namespace: `Template.Service.Services.Infrastructure.Http`.
- Autofac DI registracija koristi novi namespace.

## Configuration / Deployment Impact

Nema konfiguracionih ili deployment promena. Izvršno ponašanje nije menjano.

## Validation

- Solution build: uspešan.
- Postojeći testovi: 12/12 uspešno.

## Further Improvements

- Kasnije razmotriti poseban project ili NuGet paket.

## Dependency Graph Update - 2026-08-18

- BusinessLogic sada direktno zavisi od `Template.Service.Services.Interfaces`, ne od konkretne `Template.Service.Services` implementacije.
- `Template.Service.DependencyInjection` eksplicitno referencira projekte čije tipove registruje.
- API više nema nepotrebne direktne reference na konkretne BusinessLogic i Services projekte.
- Nema promene runtime ponašanja; build je uspešan i svi testovi prolaze (21/21).

