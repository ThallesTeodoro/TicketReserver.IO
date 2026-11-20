# Design: TR.HW.ResTimeout

## Context

See [proposal.md](proposal.md) for motivation. The reservation system locks seats in Redis with a 10-minute TTL and records `AWAITING_PAYMENT` holds in PostgreSQL. The Outbox Relay produces a delayed message to Apache Kafka (`reservation.v1.hold_created.delayed`). This design establishes the dedicated worker service `TR.HW.ResTimeout` to consume that stream and guarantee state convergence between PostgreSQL and Redis.

## Goals / Non-Goals

**Goals:**
- Provide a robust .NET 10 background worker consuming from `reservation.v1.hold_created.delayed`.
- Deduplicate processing using PostgreSQL `inbox_messages` (`message_id`, `consumer_group`).
- Transition expired `AWAITING_PAYMENT` reservations to `EXPIRED` with reason `PAYMENT_TIMEOUT`.
- Idempotently release Redis seat keys `hold:event:{eventId}:seat:{seatId}` using safe atomic Lua scripts.
- Extract W3C `traceparent` headers to correlate logs and traces across services.
- Follow Clean Architecture, zero MediatR, zero AutoMapper, strict configuration validation (fail-fast).
- Ensure 100% unit test coverage of worker logic, idempotency, and repository queries.

**Non-Goals:**
- Handling payment capture or refunds (handled by `TR.HW.Payment` and `TR.HW.Refund`).
- Handling user-initiated cancellations via HTTP (handled by `TR.API.Reservation`).
- Rescheduling Kafka delayed messages internally (Apache Kafka Delayed/TTL topic handles the delivery timing).

## Decisions

### 1. Worker Architecture & Technology Stack
- **Choice**: .NET 10 Worker Service (`Microsoft.Extensions.Hosting`) using Dapper / Npgsql for PostgreSQL and `StackExchange.Redis` for cache/locks.
- **Rationale**: Minimal footprint, ultra-low latency, full alignment with `TR.HW.ReservationOutboxRelay`.
- **Alternatives Considered**: 
  - Quartz.NET / Hangfire polling: Rejected due to heavy database polling contention under high reservation volumes.
  - Direct Redis keyspace notifications (`__keyevent@0__:expired`): Rejected because Redis keyspace events are pub/sub without persistence, at-least-once delivery, or delivery guarantees if the worker restarts.

### 2. Idempotency via PostgreSQL Inbox Pattern
- **Choice**: Query and record `(message_id, consumer_group)` in `inbox_messages` inside the same transaction that updates `reservations`.
- **Rationale**: Guarantees atomic state transition and duplicate suppression even if Kafka redelivers the message after a worker crash prior to offset commit.
- **Alternatives Considered**: 
  - Redis set for message IDs: Rejected because cross-system distributed 2PC would be required to keep Redis and PostgreSQL consistent.

### 3. Safe Redis Lock Release with Lua Script
- **Choice**: Execute atomic Lua script on each seat key:
  ```lua
  if redis.call("get", KEYS[1]) == ARGV[1] then
      return redis.call("del", KEYS[1])
  else
      return 0
  end
  ```
- **Rationale**: If a hold expired and another customer immediately acquired a hold on the same seat, a naive `DEL` would evict the new customer's valid hold. The Lua script guarantees deletion only when the value strictly equals the expired reservation ID.

### 4. Confluent.Kafka Manual Offset Commit
- **Choice**: `EnableAutoCommit = false`. Manually commit offsets only after PostgreSQL transaction and Redis Lua releases complete.
- **Rationale**: Guarantees at-least-once processing semantics without message loss.

## Risks / Trade-offs

- **[Risk] High load during flash sale expiration bursts** → *Mitigation*: Batch fetching and parallel Redis evaluation where applicable, combined with Dapper connection pooling and lightweight async I/O.
- **[Risk] Network partition between Worker and Redis during expiration** → *Mitigation*: The reservation status is already set to `EXPIRED` in PostgreSQL. Redis hold keys have a native TTL (600 seconds) as a fallback safety net, ensuring the seat cannot remain permanently locked.
- **[Risk] Poison pill messages in Kafka** → *Mitigation*: Deserialization errors or invalid payloads are routed to DLQ (`reservation.v1.hold_created.delayed.DLQ`) after retry threshold, preventing partition stalls.

## Migration & Deployment Plan

1. Create `TR.HW.ResTimeout` worker project and unit test suite.
2. Add Dockerfile and register `tr-hw-restimeout` in root `docker-compose.yml`.
3. Verify topics in Kafka stack (`reservation.v1.hold_created.delayed` exists with partition configuration).
4. Run integration and unit tests validating consumption, idempotency, and Redis release.
