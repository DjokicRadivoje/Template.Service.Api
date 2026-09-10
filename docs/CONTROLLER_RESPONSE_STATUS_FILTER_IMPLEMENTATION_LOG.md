# Centralizovano Response HTTP status mapiranje - Implementation Log

## Context

- Ticket: Nije naveden
- Branch: `main`
- Date: 2026-08-18
- Analysis: `docs/CONTROLLER_RESPONSE_STATUS_FILTER_ANALIZA_I_DOGOVOR.md`

## Timeline

- 2026-08-18 - Dogovoreno očuvanje postojećeg status mapiranja i globalna primena na `Response<T>`.
- 2026-08-18 - Implementirani `IResponse`, resolver, globalni result filter i čist controller.
- 2026-08-18 - Dodat `Template.Service.Api.Tests` sa 9 novih testova.
- 2026-08-18 - Izvršeni solution build, svi testovi i lokalna runtime provera.

## Implementation Notes

- `IResponse` omogućava negeneričku proveru standardnog response-a bez reflection-a.
- Resolver čuva postojeći prioritet i statuse 200/500/504/502.
- Filter menja samo standardni ObjectResult sa inicijalnim statusom 200.
- `ProductController.GetProduct` sada samo vraća `Ok(await ...)`.

## Problems / Bugs

### Missing test using

- Symptom: Prvi API test build nije prepoznao `StatusCodes` u resolver testu.
- Cause: Nedostajao je `Microsoft.AspNetCore.Http` using.
- Fix: Dodat je odgovarajući using.
- Status: Fixed and verified.

### Runtime validation request

- Observation: Prvi lokalni curl payload proizveo je automatski HTTP 400 pre controllera.
- Resolution: Validan JSON zahtev je ponovljen i prošao je action/filter pipeline sa HTTP 200.
- Status: Expected ASP.NET model-validation behavior; nije application bug.

## Validation Log

- `dotnet test Template.Service.Api.Tests/Template.Service.Api.Tests.csproj -c Debug --no-restore` - passed, 9/9.
- `dotnet build Template.Service.Api.sln -c Debug --no-restore` - passed, 0 errors, 0 warnings.
- `dotnet test Template.Service.Api.sln -c Debug --no-build --no-restore` - passed, 21/21.
- Runtime POST `/Product` - passed kroz MVC pipeline, HTTP 200.

## Commits

- Pending commit.

## Deployment / Environment

- Nema novih konfiguracionih ključeva ili migracija.
- Runtime HTTP status za standardni `Response<T>` sada se centralno određuje filterom.
- History: Osvežen `C:\Users\Radivoje\.codex\skills\History\controller-response-status-filter`.

## Follow-up

- Razmotriti model-validation response standardizaciju i OpenAPI status konvencije u zasebnim koracima.

