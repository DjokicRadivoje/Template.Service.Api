# Centralizovano Response HTTP status mapiranje

## Summary

HTTP status logika je uklonjena iz controllera i centralizovana u globalnom MVC result filteru.

## Implemented Changes

- `Response<T>` implementira negenerički `IResponse` ugovor.
- Centralni resolver čuva postojeće 200/500/504/502 mapiranje.
- Globalni filter obrađuje svaki standardni response sa inicijalnim statusom 200.
- `ProductController.GetProduct` je sveden na `Ok(await ...)`.
- Dodat je API test projekat sa 9 testova.

## Configuration / Deployment Impact

Nema novih konfiguracionih ključeva ni migracija.

## Validation

- Build: uspešan, 0 grešaka.
- Testovi: 21/21 uspešno.
- Runtime Product poziv: uspešan.

## Further Improvements

- Standardizacija model-validation 400 i OpenAPI status konvencija ostaju van trenutnog scope-a.
