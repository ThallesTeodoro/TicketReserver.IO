# Design: TR.HW.Payment (Payment Processing Worker)

## Context

See [proposal.md](proposal.md) for motivation. The reservation system emits `reservation.v1.checkout_req` when a customer triggers `POST /reserva/checkout`. This design establishes `TR.HW.Payment` as a modular Clean Architecture .NET 10 solution separated into strict layers (Domain, Application, Infrastructure, Worker), adhering to project standards (CQRS Lite, zero MediatR, zero AutoMapper, Dapper/Npgsql persistence, Polly resilience, and manual Kafka offset commits).

## Goals / Non-Goals

**Goals:**
- Implement a 4-layer architecture separating business rules, application orchestration, external infrastructure, and background execution:
  - `TR.HW.Payment.Domain`: Rich aggregates, entities, enums, financial invariants, and domain events.
  - `TR.HW.Payment.Application`: CQRS Lite commands and handlers (zero MediatR), FluentValidation, and static mapping methods (zero AutoMapper).
  - `TR.HW.Payment.Infrastructure`: PostgreSQL Dapper persistence with transactional integrity, Inbox/Outbox patterns, Polly-resilient payment gateway adapter, and Kafka producer.
  - `TR.HW.Payment.Worker`: Worker service entrypoint hosting the background Kafka consumer with manual commits and W3C trace propagation.
- Guarantee at-least-once processing without duplicate payments via the Inbox pattern and `uq_payments_reservation_id` unique constraint.
- Isolate external gateway communication behind resilient policies (retry with exponential backoff and jitter).
- Deliver 100% unit test coverage in `TR.HW.Payment.UnitTests`.

**Non-Goals:**
- Implementing refunds and sagas for ticketing failures (handled by `TR.HW.Refund`).
- Serving HTTP endpoints (all interactions are event-driven via Kafka).

## Decisions

### 1. Layered Project Architecture
- **Choice**:
  - `TR.HW.Payment.Domain`: Zero external dependencies, pure business logic.
  - `TR.HW.Payment.Application`: References `Domain`, contains use case handlers and abstractions.
  - `TR.HW.Payment.Infrastructure`: References `Application`, implements Dapper repositories, Polly gateway clients, and Kafka producers.
  - `TR.HW.Payment.Worker`: References `Application` and `Infrastructure`, configures DI and runs `BackgroundService`.
  - `TR.HW.Payment.UnitTests`: Test suite covering all layers.
- **Rationale**: Strict separation of concerns, testability, and alignment with the `.agents/skills/dotnet-clean-cqrs-scaffold` guidelines.

### 2. CQRS Lite & Explicit Mapping
- **Choice**: Handcrafted `ICommand<TResponse>` and `ICommandHandler<TCommand, TResponse>` interfaces; static extension methods for DTO mappings.
- **Rationale**: Eliminates runtime reflection, assembly-scanning bugs, and third-party dependencies (`MediatR`, `AutoMapper`).

### 3. Payment Gateway Resilience with Polly
- **Choice**: Wrap the gateway HTTP client with Polly `AsyncRetryPolicy` configured with 3 retries, exponential backoff (2s, 4s, 8s), and random jitter.
- **Rationale**: Handles transient network glitches without failing legitimate checkouts, while non-retryable errors (e.g. card declined) fail fast without retrying.

### 4. Transactional Inbox and Outbox Pattern
- **Choice**: In a single PostgreSQL transaction:
  1. Record `payments` record (`status = 'CAPTURED'` or `'DECLINED'`).
  2. Insert immutable audit log into `payment_transactions`.
  3. Insert outgoing event into `outbox_messages` (`payment.v1.approved` or `payment.v1.declined`).
  4. Record processed message ID into `inbox_messages`.
- **Rationale**: Atomic state transition prevents inconsistencies if the process crashes before offset commit.

### 5. Manual Kafka Offset Commit
- **Choice**: `EnableAutoCommit = false`. Commit offset only after local database transaction completes.
- **Rationale**: Prevents lost checkout events in failure scenarios.

## Risks / Trade-offs

- **[Risk] Gateway timeouts leaving transaction state ambiguous** → *Mitigation*: The payment gateway client sends idempotency keys (`reservationId`) so that retries or reconciliation calls return the identical authorization without double charging.
- **[Risk] Outbox message publication delay** → *Mitigation*: The worker dispatches the event immediately to Kafka upon successful database commit and marks the outbox message as published.
- **[Risk] Database schema availability for payment_db** → *Mitigation*: Provide `./docker/postgres/init-payment-db.sql` mounted into PostgreSQL container to initialize tables and enums idempotently.

## Migration & Deployment Plan

1. Create `init-payment-db.sql` script for PostgreSQL container.
2. Scaffold `TR.HW.Payment` multi-project solution and link references.
3. Implement layers: Domain -> Application -> Infrastructure -> Worker.
4. Add unit test suite with 100% pass rate.
5. Create Dockerfile and register `tr-hw-payment` in Compose stack.
