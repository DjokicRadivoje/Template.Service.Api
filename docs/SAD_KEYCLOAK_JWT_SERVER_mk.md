# Keycloak JWT серверска заштита на Service.Api.Template

## Резиме

Сите постојни и идни `Service.Api.Template` controller endpoint-и сега стандардно бараат валиден Keycloak JWT access token за audience `service-api-template`.

## Имплементирани промени

- Додаден е JWT Bearer handler за .NET 10.
- Се валидираат issuer, audience, lifetime и signing key.
- Воведена е fail-fast валидација на Keycloak конфигурацијата.
- Глобална fallback authorization политика бара автентициран корисник.
- `UseAuthentication()` се извршува пред `UseAuthorization()`.
- Development Swagger поддржува внес на Bearer token.
- Authentication логовите не содржат token, Authorization header или claim вредности.

## Тек на барањето

```text
HTTP барање
  -> JWT Bearer автентикација
  -> глобална RequireAuthenticatedUser политика
  -> controller/action
```

Недостасувачки или невалиден token враќа `401`. Идно role/policy одбивање за валидно автентициран повикувач ќе враќа `403`.

## Конфигурација

Секоја околина мора да ги постави `Authentication:Keycloak` вредностите: HTTPS `Authority`, `Audience`, `RoleClientId` и `ClockSkewSeconds`. Тековниот audience е `service-api-template`, со почетен clock skew од 60 секунди.

## Валидација

- Build: passed, 0 грешки.
- Тестови: 40/40 passed.
- Валиден локален test token: HTTP 200 на probe controller-от.
- Без token, погрешен issuer/audience/signature или истечен token: HTTP 401.
- Runtime: Swagger HTTP 200; `POST /Product` без token HTTP 401.

## Отворени точки

- Реален Authority по околина и реална Keycloak E2E валидација.
- Audience mapper конфигурација на caller client-ите.
- Идни role/policy одлуки и експлицитно одобрен анонимен health endpoint, ако е потребен.

