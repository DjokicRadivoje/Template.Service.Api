# Shared CRM Error Mapping - Analysis

## Source and Scope

- Contract source: `docs/CRM.api/swagger20260902.json`, version dated September 2, 2026.
- Covered CRM methods: RSM-130, RSM-133, RSM-147, RSM-150, and RSM-155, plus future `/Api/V8/custom/*` methods using the same error contract.
- Branch: `development`.
- Analysis date: September 2, 2026.
- This document defines the shared architectural direction. Implementation will be introduced incrementally through individual tickets, starting with RSM-149/RSM-150 mapping.

## Goal

The service layer should contain as little business logic as possible. Its responsibilities are:

- sending HTTP requests;
- deserializing success or error payloads;
- preserving the HTTP/transport outcome;
- technical logging without exposing sensitive or internal details.

The BusinessLogic layer owns:

- mapping a CRM failure to the public Service API Template status and message code;
- endpoint-specific business decisions;
- safe text returned to the client;
- deciding whether a specific CRM `detail` changes the business result.

## Swagger Findings

All currently documented `/Api/V8/custom/*` methods use the shared `#/components/schemas/Error` contract:

```json
{
  "code": "VALIDATION_ERROR | NOT_FOUND | BUSINESS_RULE | UNAUTHORIZED | FORBIDDEN | INTERNAL_ERROR",
  "message": "CRM error message",
  "correlationId": "CRM body value",
  "detail": "endpoint-specific-domain-code"
}
```

By agreement, `Correlation-ID` is used exclusively as an HTTP header. The `correlationId` value from the CRM error body is not propagated to the Service API Template body and is not part of business mapping.

Methods differ in HTTP statuses and `detail` values, but not in error-object structure:

| CRM method | Statuses | Example `detail` values |
|---|---|---|
| RSM-130 subscription upsert | `400`, `401`, `403`, `422`, `500` | `product_not_found`, `customer_not_found`, `invalid_status` |
| RSM-150 customer upsert | `400`, `401`, `403`, `422`, `500` | `missing_last_name`, `invalid_source_occurred_at` |
| RSM-155 KPU registration | `400`, `401`, `403`, `404`, `422`, `500` | `kpu_product_not_found`, `kpu_customer_not_found`, `invalid_registered_at` |
| RSM-147 account search | `401`, `403`, `422`, `500` | `invalid_page`, `invalid_page_size` |
| RSM-133 subscription transition | `400`, `401`, `403`, `404`, `422`, `500` | `invalid_action`, `invalid_state_transition`, `override_info_required` |

Some Swagger `401`, `403`, and `500` response definitions do not repeat the `$ref` to the Error schema, but the main Swagger description confirms that every `/Api/V8/custom/*` method returns the unified Error object on failure.

## Architectural Decision

Use one shared CRM error transport model and one shared HTTP response reader. Do not introduce transport error models or HTTP handlers per method.

Shared mapping of canonical CRM failures to safe Service API Template errors belongs to the BusinessLogic layer. Each BusinessLogic method supplies its public message codes and, only when necessary, endpoint-specific rules for CRM `detail`.

```text
CRM HTTP response
  -> shared CRM response reader
     -> CrmServiceResult<T> + CrmErrorResponse
        -> shared BusinessLogic error mapper/helper
           -> endpoint-specific public code/status/text
              -> Response<TBusiness>
```

## Proposed Components

### CRM transport error model

Proposed location:

```text
Template.Service.DataModel/Crm/CrmErrorResponse.cs
```

Minimal fields required for internal mapping:

- `Code`;
- `Message`;
- `Detail`.

Body `CorrelationId` is not used; correlation is handled exclusively through the existing `Correlation-ID` header pipeline.

### CRM service-call result

Proposed contract location:

```text
Template.Service.Services.Interfaces/Infrastructure/Http/CrmServiceResult.cs
```

The result should carry facts without making business decisions:

- success DTO or `null`;
- HTTP status;
- deserialized `CrmErrorResponse` or `null`;
- transport failure type, such as timeout, unavailable, or deserialization failure;
- success indicator derived from the HTTP/transport result.

`CrmServiceResult<T>` is not a public API response and is never serialized to the client.

### Shared CRM response reader

Proposed location:

```text
Template.Service.Services/Infrastructure/Http/CrmResponseHandler.cs
```

Responsibilities:

- deserialize any success DTO for `2xx`;
- deserialize the shared `CrmErrorResponse` for non-`2xx`;
- preserve HTTP status and transport failure;
- support an `ok/error` success envelope where a CRM method actually uses it;
- never choose `CUSTOMER_DATA_INVALID`, `SUBSCRIPTION_NOT_FOUND`, or other Service API Template codes;
- never copy raw CRM `message` or `detail` into public `Response.Messages`.

The existing `where T : CrmApiResponse` generic constraint should be removed or replaced with an optional marker interface for methods whose success DTO really has an `ok/error` envelope. Flat success DTOs for RSM-147, RSM-150, and RSM-155 must not be classified as failures because they do not contain `ok`.

### BusinessLogic error mapper

Proposed location:

```text
Template.Service.BusinessLogic/Infrastructure/CrmErrorMapper.cs
```

Alternatively, the existing `BusinessLogicResponseHelper` may be extended if the result remains readable and testable.

Responsibilities of the shared BusinessLogic mapper:

- map HTTP status and canonical CRM `code` to `ResponseStatus`;
- select safe public text;
- add the public endpoint-specific message code;
- prevent propagation of raw CRM `message`, `detail`, and body correlation ID;
- support an optional endpoint policy map only when `detail` represents a business-relevant distinction.

A conceptual customer BusinessLogic call should look like:

```text
CrmErrorMapper.Apply(
    serviceResult,
    businessResponse,
    validationCode: CUSTOMER_DATA_INVALID,
    failureCode: CUSTOMER_UPSERT_FAILED)
```

## Shared Versus Endpoint-Specific Mapping

| Responsibility | Shared | Per method |
|---|---:|---:|
| Deserialize unified Error JSON | Yes | No |
| Handle timeout/unavailable/deserialization failures | Yes | No |
| Recognize canonical CRM `code` | Yes | No |
| Prevent CRM `message/detail` leakage | Yes | No |
| Select public Service API Template message code | No | Yes, through BusinessLogic parameters/policy |
| Map a special `detail` code that changes the business outcome | No | Yes, only when concretely required |
| Write a separate error handler for each endpoint | No | No |

RSM-149 does not need special `detail` rules. `400` and CRM validation outcomes map to public `CUSTOMER_DATA_INVALID`, while other integration failures use `CUSTOMER_UPSERT_FAILED`. Duplicate and company-ambiguity outcomes arrive as successful HTTP `200` responses with statuses and warnings and therefore do not belong to the error mapper.

## Impact on Existing Code

- `ICrmService` methods currently return `BusinessModel.Common.Response<T>`, mixing transport and business results.
- `BusinessLogicResponseHelper.ApplyServiceFailure` currently copies every service message into the public response.
- `HttpResponseHandler` discards the CRM error body for non-success responses.
- `CrmResponseHandler` expects a `CrmApiResponse` `ok/error` envelope and does not support every flat success DTO in Swagger.

The recommended direction is to migrate real CRM methods incrementally to `CrmServiceResult<T>`. Migration can start with RSM-149/RSM-150 and then be applied to other methods when their real CRM mapping is implemented or updated.

## Implementation Order

- [x] Add the shared `CrmErrorResponse` transport model.
- [x] Define `CrmServiceResult<T>` without a dependency on the public Service API Template response contract.
- [x] Generalize the CRM response handler for flat success, optional `ok/error` envelopes, and unified error responses.
- [x] Add a shared BusinessLogic `CrmErrorMapper`.
- [x] Apply the new flow to RSM-149/RSM-150 first.
- [ ] After validation, migrate RSM-130, RSM-133, RSM-147, and RSM-155 when their service integrations are implemented or updated.
- [ ] Remove old or duplicated handler flows only after they have no remaining consumers.

## Test Plan

- [x] Shared handler: canonical CRM error codes are deserialized.
- [x] Shared handler: `400`, `401`, `403`, `404`, `422`, and `500` preserve the HTTP status.
- [x] Shared handler: a flat success response is not treated as a failure because `ok` is absent.
- [x] Shared handler: existing `ok/error` envelope tests continue to work.
- [x] BusinessLogic mapper: internal CRM `message`, `detail`, and body correlation ID do not reach the public response.
- [x] BusinessLogic mapper: RSM-149 receives its public validation/failure code.
- [x] RSM-149 flow tests: validation and integration-failure mapping.
- [x] Regression tests for the already active RSM-130 service integration through the full solution suite.

## Risks and Safeguards

| Risk | Safeguard |
|---|---|
| Changing the `ICrmService` return type affects multiple BusinessLogic classes and test stubs. | Migrate one method at a time and temporarily retain the old adapter where required. |
| Raw CRM error details may leak through `Response.Messages`. | Never copy them directly; only the BusinessLogic mapper creates public messages. |
| RSM-130/RSM-133 use `ok/error`, while other methods use flat success. | Check an optional marker/interface only for DTOs that implement it. |
| `detail` codes may grow without changing the Swagger Error schema. | Map an unknown `detail` to the safe endpoint failure code and log it structurally. |
| A broad migration would increase RSM-149 risk. | Implement the shared foundation and customer vertical in RSM-149; migrate other methods separately. |

## Status

- Architectural direction agreed.
- The shared foundation was implemented on September 2, 2026, and applied to RSM-149/RSM-150.
- Other CRM methods still use their existing flows and will migrate through their own tickets.


