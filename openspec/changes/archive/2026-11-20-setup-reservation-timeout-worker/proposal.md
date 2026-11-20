# Proposal

## Why

When a customer holds tickets via `POST /reserva/hold`, seats are locked in Redis with a 10-minute TTL and recorded in PostgreSQL with status `AWAITING_PAYMENT`. The Outbox Relay dispatches `reservation.v1.hold_created.delayed` with a 10-minute delivery delay. Without a dedicated timeout worker consuming this delayed stream, unpaid reservations would remain stranded in `AWAITING_PAYMENT` indefinitely in PostgreSQL after Redis keys expire, leading to data inconsistency and inventory lockup.

## What Changes

- Implement `TR.HW.ResTimeout` (.NET 10 Worker Service) dedicated to consuming delayed reservation hold events from Apache Kafka.
- Implement Idempotent Consumer (Inbox pattern) using PostgreSQL `inbox_messages` table to guarantee exactly-once business processing.
- Query reservation status upon message arrival:
  - If `AWAITING_PAYMENT`: atomically transition status to `EXPIRED` with cancellation reason `PAYMENT_TIMEOUT`.
  - If `PROCESSING_PAYMENT` or `CONFIRMED`: ignore expiration safely (customer started checkout or already paid).
- Release active Redis seat locks (`hold:event:{eventId}:seat:{seatId}`) using atomic Lua script matching reservation ownership.
- Extract W3C `traceparent` headers from Kafka messages to ensure continuous end-to-end distributed tracing.
- Manual offset commit (`EnableAutoCommit = false`) after successful local transaction commit.
- Provide comprehensive test coverage with xUnit and integrate worker into root `docker-compose.yml`.

## Capabilities

### New Capabilities
- `reservation-timeout-worker`: Consumes delayed reservation hold events, performs idempotent reconciliation, expires unpaid holds in PostgreSQL, and releases Redis seat hold locks.

### Modified Capabilities
<!-- None. Existing specifications remain intact. -->

## Impact

- **Services**: New worker project `TR.HW.ResTimeout` under `.NET 10`.
- **Databases**: `reservation_db` (updates `reservations`, reads `reservation_items`, writes `inbox_messages`).
- **Cache/Distributed Lock**: Redis instance (`RedisHold`) keys cleaned via Lua script.
- **Messaging**: Apache Kafka topic `reservation.v1.hold_created.delayed` consumed by consumer group `cg-reservation-timeout`.
- **Observability & Deploy**: OpenTelemetry tracing & metrics, health checks, Dockerfile and `docker-compose.yml` service definition.
