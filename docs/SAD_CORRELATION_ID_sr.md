# Correlation ID

## Summary

API sada koristi jedan `Correlation-ID` kroz ulazni zahtev, obradu, izlazne HTTP pozive, logove i HTTP odgovor.

## Implemented Changes

- Prihvata se isključivo GUID u `D` formatu; nevalidna vrednost se zamenjuje novim ID-em uz warning.
- Middleware postavlja finalni ID u `HttpContext.TraceIdentifier`, request i response header.
- `DelegatingHandler` automatski prosleđuje isti ID kroz default `HttpClient` pipeline.
- Serilog file sink prikazuje strukturirano svojstvo `CorrelationId`.
- Stari `X-UniqueTrace-Id` mehanizam je uklonjen.

## Affected Components

- `Framework.Logger/Correlation` - middleware, handler, konstante i DI extensions.
- `Template.Service.Api/Program.cs` - registracija correlation pipeline-a.
- `Template.Service.Api/appsettings.json` - format log izlaza.
- `Framework.Logger.Tests` - 12 automatskih testova.

## Configuration / Deployment Impact

Nema novih konfiguracionih ključeva niti migracija. Promenjen je tekstualni Serilog output template. Klijenti treba da koriste header `Correlation-ID`.

## Validation

- Solution Debug build: uspešan, 0 grešaka.
- Testovi: 12/12 uspešno.
- Runtime response i Serilog correlation provera: uspešna.

## Further Improvements

- Budući background poslovi sa više downstream poziva treba da otvore zajednički correlation scope.

