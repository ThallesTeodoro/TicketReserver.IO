# Proposal

## Why

When ticket issuance fails due to concurrent seat allocation collisions (`ticketing.v1.failed`), the customer's already captured payment must be automatically refunded (*Auto-Refund* compensation saga) to maintain financial integrity and trigger reservation cancellation downstream.

## What Changes

- Scaffold `TR.HW.Refund` solution with clean separation across 5 projects (`Domain`, `Application`, `Infrastructure`, `Worker`, `UnitTests`) following .NET 10 and CQRS Lite guidelines (zero MediatR and zero AutoMapper).
- Implement `Refund` aggregate root, `PaymentToRefund` read/state entity, `RefundTransaction` ledger entity, and `PaymentRefundedDomainEvent` in `TR.HW.Refund.Domain`.
- Implement CQRS Lite `ProcessRefundCommand`, `ProcessRefundCommandHandler`, and `ProcessRefundCommandValidator` orchestrating idempotency checks, payment verification, gateway refund execution, and atomic state mutation.
- Implement `RefundRepository` using Dapper and Npgsql operating against `payment_db` (`payments`, `refunds`, `payment_transactions`, `outbox_messages`, and `inbox_messages`).
- Implement `RefundGatewayService` with Polly resilience pipeline (retry with exponential backoff and random jitter) to execute external gateway void/refund calls.
- Implement `KafkaRefundProducer` with W3C `traceparent` context propagation, publishing `payment.v1.refunded` and providing DLQ fallback.
- Implement `PaymentRefundWorker` (`BackgroundService`) consuming `ticketing.v1.failed` under consumer group `cg-payment-refund` with manual offset acknowledgment.
- Configure multi-stage `Dockerfile` and `docker-compose.yml` for `TR.HW.Refund`, registering it in the root `docker-compose.yml`.

## Capabilities

### New Capabilities
- `payment-refund-worker`: Consumes ticket issuance failure events, executes resilient payment gateway refunds, updates payment and refund ledgers transactionally in `payment_db`, and dispatches `payment.v1.refunded` events.

### Modified Capabilities
<!-- None -->

## Impact

- **New Projects**: `TR.HW.Refund` (`Domain`, `Application`, `Infrastructure`, `Worker`, `UnitTests`).
- **Database**: Uses existing tables `payments`, `refunds`, `payment_transactions`, `outbox_messages`, and `inbox_messages` in `payment_db`.
- **Kafka**: New consumer for `ticketing.v1.failed` (`cg-payment-refund`) and producer for `payment.v1.refunded` and `ticketing.v1.failed.DLQ`.
- **Root Docker Compose**: Registration of `ticketreserver-refund-worker` via `include`.
