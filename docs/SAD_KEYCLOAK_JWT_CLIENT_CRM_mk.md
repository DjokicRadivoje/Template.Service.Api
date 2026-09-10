# Keycloak JWT клиент и CRM.api интеграција

## Резиме

`Service.Api.Template` сега добива и кешира Keycloak access token преку `client_credentials` текот и го додава исклучиво на излезните повици од named client-от `CRM.api`. Додаден е заштитен CRM mock тек според постојната слоевита архитектура.

## Имплементирани промени

- Одделни `KeycloakToken` и `CRM.api` named clients.
- Concurrency-safe singleton token cache со рано освежување.
- CRM-only Bearer handler со условна token invalidation при `401`.
- Product/HR и token client-от не добиваат CRM Authorization header.
- Нов `GET /api/Crm/mock` controller/business/service тек кон привремената downstream патека `api/mock/customers`.
- Fail-fast HTTPS/options/secret валидација и редакција на Authorization header-от.

## Влијание врз конфигурација и deployment

Потребни се `CRMApiBaseAddress`, Keycloak token URL, client ID, client secret и refresh window. Client secret-от мора да доаѓа од User Secrets, environment променлива или продукциски secret provider и не се наоѓа во appsettings датотеките.

## Валидација

- Build: passed, 0 грешки.
- Тестови: 59/59 passed.
- Потврдени се cache, refresh, concurrency, invalidation, cancellation и named-client isolation сценаријата.
- Runtime: Swagger HTTP 200; CRM mock без inbound JWT враќа HTTP 401.

## Понатамошни подобрувања

- Да се изврши E2E валидација со реален Keycloak token и CRM сервис.
- Да се потврди audience/role мапирањето и mock патеката да се замени со реалниот CRM договор.
- Retry да се воведе само за потврдени idempotent повици.

