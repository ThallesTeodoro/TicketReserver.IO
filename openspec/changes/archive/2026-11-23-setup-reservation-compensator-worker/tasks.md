# Tasks

## 1. Solution Scaffolding & Project Setup

- [x] 1.1 Scaffold `TR.HW.ResCompensator` solution (`.sln` and `.slnx`) with 5 projects (`Domain`, `Application`, `Infrastructure`, `Worker`, `UnitTests`), configure layer references, and verify `dotnet build` succeeds.

## 2. Domain Layer Implementation

- [x] 2.1 Implement `ReservationState` aggregate root, `ReservedSeatItem` entity, enums (`ReservationStatus`, `ReservationOutcomeType`), and domain exceptions in `TR.HW.ResCompensator.Domain`.
- [x] 2.2 Implement unit tests in `TR.HW.ResCompensator.UnitTests/Domain` verifying invariant rules, confirmation transitions, cancellation transitions, and idempotency guards.

## 3. Application Layer (CQRS Lite & Use Cases)

- [x] 3.1 Implement CQRS Lite interfaces (`ICommand`, `ICommandHandler`), `ReconcileReservationOutcomeCommand`, `ReconcileReservationOutcomeCommandHandler`, and `ReconcileReservationOutcomeCommandValidator` in `TR.HW.ResCompensator.Application`.
- [x] 3.2 Implement domain/DTO static mapping extension methods (zero AutoMapper) and port interfaces (`IReservationCompensationRepository`, `IRedisSeatLockReleaser`).
- [x] 3.3 Implement unit tests in `TR.HW.ResCompensator.UnitTests/Application` verifying confirmation flow, refund cancellation with Redis seat release, decline cancellation with Redis seat release, and missing reservation handling.

## 4. Infrastructure Layer (PostgreSQL Persistence & Redis Lua Lock Release)

- [x] 4.1 Implement `ReservationCompensationRepository` using Dapper and Npgsql against `reservation_db` (`reservations`, `reservation_items`, `inbox_messages`) in `TR.HW.ResCompensator.Infrastructure`.
- [x] 4.2 Implement `RedisSeatLockReleaser` using `StackExchange.Redis` and an atomic Lua script to release seat keys owned by the cancelled reservation.
- [x] 4.3 Implement unit tests in `TR.HW.ResCompensator.UnitTests/Infrastructure` verifying Redis Lua script execution and key formatting.

## 5. Worker Service & Integration

- [x] 5.1 Implement `ReservationCompensationWorker` (`BackgroundService`) subscribing to `payment.v1.refunded`, `payment.v1.declined`, and `ticketing.v1.issued` under consumer group `cg-reservation-compensator`, checking inbox deduplication, executing command handler, and committing Kafka offset manually.
- [x] 5.2 Configure `DependencyInjection.cs`, `Program.cs`, and `appsettings.json` with fail-fast options validation throwing `ArgumentNullException` on missing parameters.
- [x] 5.3 Implement unit tests in `TR.HW.ResCompensator.UnitTests/Workers` and `Configuration` verifying multi-topic message parsing, DLQ routing on poisoned payloads, and configuration fail-fast validation.

## 6. Stack Integration & Verification

- [x] 6.1 Create `Dockerfile` and `docker-compose.yml` for `TR.HW.ResCompensator` and register `tr-hw-res-compensator` in root `docker-compose.yml`.
- [x] 6.2 Execute `dotnet test` across all solution test suites in the repository, validate OpenSpec specs with `openspec validate --specs`, and verify zero warnings.
