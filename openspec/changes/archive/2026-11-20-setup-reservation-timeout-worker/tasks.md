# Tasks

## 1. Solution & Project Scaffolding

- [x] 1.1 Create `TR.HW.ResTimeout` solution (`.sln` and `.slnx`) and .NET 10 Worker Service project with dependencies (`Confluent.Kafka`, `Dapper`, `Npgsql`, `StackExchange.Redis`, `OpenTelemetry`), and verify `dotnet build` succeeds.
- [x] 1.2 Create `TR.HW.ResTimeout.UnitTests` test project with xUnit, Moq, and FluentAssertions, add project references, and verify `dotnet test` executes with zero errors.

## 2. Configuration & Dependency Injection

- [x] 2.1 Implement strongly-typed options `KafkaConsumerOptions`, `DatabaseOptions`, and `RedisOptions` with strict validation throwing `ArgumentNullException` on missing settings, and verify with unit tests.
- [x] 2.2 Implement `DependencyInjection.cs` registering connection factories, Redis multiplexer, options bindings, health checks, and OpenTelemetry tracing sources.

## 3. Data Access & Redis Seat Lock Management

- [x] 3.1 Implement `IReservationTimeoutRepository` and `ReservationTimeoutRepository` with Dapper queries for `inbox_messages` check, reservation status retrieval, and atomic update to `EXPIRED` (`cancellation_reason = 'PAYMENT_TIMEOUT'`) in a single transaction.
- [x] 3.2 Implement `ISeatLockManager` and `SeatLockManager` using `StackExchange.Redis` executing an atomic Lua script to release `hold:event:{eventId}:seat:{seatId}` only if the value equals the reservation ID.

## 4. Kafka Consumer & Worker Service

- [x] 4.1 Implement `IKafkaConsumer` wrapper handling consumer lifecycle, deserialization, W3C `traceparent` extraction, DLQ fallback, and manual offset commit.
- [x] 4.2 Implement `ReservationTimeoutWorker` (`BackgroundService`) orchestrating consumption loop, inbox deduplication, status evaluation, atomic expiration, Redis lock release, and manual offset acknowledgment.

## 5. Unit Testing & Verification

- [x] 5.1 Implement unit tests for `ReservationTimeoutRepository` and `SeatLockManager` verifying SQL queries, parameter bindings, and Lua script execution.
- [x] 5.2 Implement comprehensive unit tests for `ReservationTimeoutWorker` verifying:
  - `AWAITING_PAYMENT` holds transition to `EXPIRED` and trigger Redis release.
  - `PROCESSING_PAYMENT` and `CONFIRMED` holds are ignored without mutation.
  - Duplicate inbox messages are skipped and immediately committed.
  - Poison pills or unexpected errors trigger DLQ or exponential backoff retry.
- [x] 5.3 Execute `dotnet test` across all solution test suites (`TR.HW.ResTimeout.UnitTests`, `TR.HW.ReservationOutboxRelay.UnitTests`, `TR.API.Reservation.UnitTests`) and verify 100% green tests with 0 warnings.

## 6. Containerization & Stack Integration

- [x] 6.1 Create multi-stage `Dockerfile` and `docker-compose.yml` for `TR.HW.ResTimeout` and register `tr-hw-restimeout` in root `docker-compose.yml`.
- [x] 6.2 Validate OpenSpec artifacts with `openspec validate --specs` and confirm the repository working tree is clean.
