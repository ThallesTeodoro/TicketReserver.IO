# Tasks

## 1. Solution Scaffolding & Project Setup

- [x] 1.1 Scaffold `TR.HW.Refund` solution (`.sln` and `.slnx`) with 5 projects (`Domain`, `Application`, `Infrastructure`, `Worker`, `UnitTests`), configure layer references, and verify `dotnet build` succeeds.

## 2. Domain Layer Implementation

- [x] 2.1 Implement `Refund` aggregate root, `PaymentSnapshot` entity, `RefundTransaction` entity, domain event (`PaymentRefundedDomainEvent`), enums, and domain exceptions in `TR.HW.Refund.Domain`.
- [x] 2.2 Implement unit tests in `TR.HW.Refund.UnitTests/Domain` verifying invariant rules (positive amount, required reason, strict status transitions, and domain event generation).

## 3. Application Layer (CQRS Lite & Use Cases)

- [x] 3.1 Implement CQRS Lite interfaces (`ICommand`, `ICommandHandler`), `ProcessRefundCommand`, `ProcessRefundCommandHandler`, and `ProcessRefundCommandValidator` in `TR.HW.Refund.Application`.
- [x] 3.2 Implement domain/DTO static mapping extension methods (zero AutoMapper) and port interfaces (`IRefundRepository`, `IRefundGatewayService`, `IKafkaRefundProducer`).
- [x] 3.3 Implement unit tests in `TR.HW.Refund.UnitTests/Application` verifying refund orchestration, already-refunded idempotency, missing payment skip behavior, and gateway failure handling.

## 4. Infrastructure Layer (Persistence, Resilient Gateway & Kafka)

- [x] 4.1 Implement `RefundRepository` using Dapper and Npgsql against `payment_db` (`payments`, `refunds`, `payment_transactions`, `outbox_messages`, `inbox_messages`) in `TR.HW.Refund.Infrastructure`.
- [x] 4.2 Implement `RefundGatewayService` with Polly resilience pipeline (3 retries with exponential backoff and jitter) and `KafkaRefundProducer` with W3C `traceparent` header propagation.
- [x] 4.3 Implement unit tests in `TR.HW.Refund.UnitTests/Infrastructure` verifying Polly retry policies and Kafka producer message formatting.

## 5. Worker Service & Integration

- [x] 5.1 Implement `PaymentRefundWorker` (`BackgroundService`) consuming `ticketing.v1.failed` under group `cg-payment-refund`, checking inbox deduplication, executing command handler, and committing Kafka offset manually.
- [x] 5.2 Configure `DependencyInjection.cs`, `Program.cs`, and `appsettings.json` with fail-fast options validation throwing `ArgumentNullException` on missing parameters.
- [x] 5.3 Implement unit tests in `TR.HW.Refund.UnitTests/Workers` and `Configuration` verifying consumption loops, DLQ routing on poisoned payloads, and configuration fail-fast validation.

## 6. Stack Integration & Verification

- [x] 6.1 Create `Dockerfile` and `docker-compose.yml` for `TR.HW.Refund` and register `tr-hw-refund` in root `docker-compose.yml`.
- [x] 6.2 Execute `dotnet test` across all solution test suites in the repository, validate OpenSpec specs with `openspec validate --specs`, and verify zero warnings.
