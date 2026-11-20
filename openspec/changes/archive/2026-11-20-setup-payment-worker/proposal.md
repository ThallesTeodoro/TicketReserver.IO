# Proposal

## Why

When customers complete the reservation checkout step (`POST /reserva/checkout`), the outbox relay publishes an immediate event to `reservation.v1.checkout_req`. To advance the ticket reservation saga, the system requires an autonomous payment processing worker (`TR.HW.Payment`) to consume checkout requests, guarantee idempotency against double charging, orchestrate payment gateway authorization/capture with network resilience (Polly), record immutable financial ledgers, and publish `payment.v1.approved` or `payment.v1.declined` events.

## What Changes

- Implement `TR.HW.Payment` structured into 4 isolated layers following Clean Architecture and CQRS Lite guidelines:
  - `TR.HW.Payment.Domain`: Rich Aggregate Root `Payment`, Entity `PaymentTransaction`, enums (`PaymentStatus`, `PaymentMethod`, `TransactionType`), domain events (`PaymentApprovedDomainEvent`, `PaymentDeclinedDomainEvent`), and financial invariants.
  - `TR.HW.Payment.Application`: CQRS Lite contracts (zero MediatR), `ProcessPaymentCommand` and handler, static mapping extension methods (zero AutoMapper), FluentValidation validators, and persistence/gateway abstractions.
  - `TR.HW.Payment.Infrastructure`: PostgreSQL Dapper persistence, Inbox deduplication (`inbox_messages`), Outbox table insertion (`outbox_messages`), resilient payment gateway client with Polly retry and jitter, and Kafka event producer.
  - `TR.HW.Payment.Worker`: BackgroundService host, Confluent.Kafka consumer (`reservation.v1.checkout_req`), W3C `traceparent` extraction, manual offset commit, and DI configuration.
- Implement comprehensive unit test suite in `TR.HW.Payment.UnitTests` covering domain invariants, application command handlers, gateway resilience, and worker orchestration.
- Provide multi-stage `Dockerfile` and `docker-compose.yml` integrated into the main Compose stack.

## Capabilities

### New Capabilities
- `payment-processing-worker`: Consumes reservation checkout requests, validates idempotency, processes gateway authorizations, records immutable payment ledgers, and emits approval/decline events.

### Modified Capabilities
<!-- None. Existing specifications remain intact. -->

## Impact

- **Services**: New distributed worker service `TR.HW.Payment` with separated layer projects (`Domain`, `Application`, `Infrastructure`, `Worker`).
- **Database**: PostgreSQL `payment_db` (`payments`, `payment_transactions`, `inbox_messages`, `outbox_messages`).
- **Messaging**: Consumes from `reservation.v1.checkout_req` (group: `cg-payment-processing`) and produces to `payment.v1.approved` / `payment.v1.declined`.
- **Infrastructure**: Containerized in Docker and integrated in root `docker-compose.yml`.
