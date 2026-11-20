# Tasks

## 1. Database Initialization & Solution Scaffolding

- [x] 1.1 Create PostgreSQL init script `docker/postgres/init-payment-db.sql` with tables `payments`, `payment_transactions`, `inbox_messages`, `outbox_messages`, enums, and unique constraint on `reservation_id`.
- [x] 1.2 Scaffold `TR.HW.Payment` solution (`.sln` and `.slnx`) with 5 projects (`Domain`, `Application`, `Infrastructure`, `Worker`, `UnitTests`), configure layer references, and verify `dotnet build` succeeds.

## 2. Domain Layer Implementation

- [x] 2.1 Implement `Payment` aggregate root, `PaymentTransaction` entity, domain events (`PaymentApprovedDomainEvent`, `PaymentDeclinedDomainEvent`), enums, and domain exceptions in `TR.HW.Payment.Domain`.
- [x] 2.2 Implement unit tests in `TR.HW.Payment.UnitTests/Domain` verifying invariant rules (positive amount, strict status transitions, immutable collection encapsulation).

## 3. Application Layer (CQRS Lite & Use Cases)

- [x] 3.1 Implement CQRS Lite interfaces (`ICommand`, `ICommandHandler`), `ProcessPaymentCommand`, `ProcessPaymentCommandHandler`, and FluentValidation validator in `TR.HW.Payment.Application`.
- [x] 3.2 Implement domain/DTO static mapping extension methods (zero AutoMapper) and port interfaces (`IPaymentRepository`, `IPaymentGatewayService`, `IKafkaPaymentProducer`).
- [x] 3.3 Implement unit tests in `TR.HW.Payment.UnitTests/Application` verifying command handling, validation rules, and approval/decline orchestration.

## 4. Infrastructure Layer (Persistence, Resilient Gateway & Kafka)

- [x] 4.1 Implement `PaymentRepository` using Dapper and Npgsql with transaction isolation, inbox deduplication check, and atomic outbox insertions in `TR.HW.Payment.Infrastructure`.
- [x] 4.2 Implement `PaymentGatewayService` with Polly resilience pipeline (retry with exponential backoff and jitter) and `KafkaPaymentProducer` with W3C `traceparent` injection.
- [x] 4.3 Implement unit tests in `TR.HW.Payment.UnitTests/Infrastructure` verifying Polly retry policies and Dapper repository mapping.

## 5. Worker Service & Integration

- [x] 5.1 Implement `PaymentCheckoutWorker` (`BackgroundService`) consuming `reservation.v1.checkout_req`, checking inbox deduplication, executing application command, and committing Kafka offset.
- [x] 5.2 Configure `DependencyInjection.cs`, `Program.cs`, and `appsettings.json` with fail-fast options validation throwing `ArgumentNullException` on missing parameters.
- [x] 5.3 Implement comprehensive unit tests in `TR.HW.Payment.UnitTests/Workers` verifying consumption loops, error handling, DLQ routing, and offset acknowledgment.

## 6. Stack Integration & Verification

- [x] 6.1 Create `Dockerfile` and `docker-compose.yml` for `TR.HW.Payment` and register `tr-hw-payment` in root `docker-compose.yml`.
- [x] 6.2 Execute `dotnet test` across all solution test suites in the repository, validate OpenSpec specs with `openspec validate --specs`, and verify zero warnings.
