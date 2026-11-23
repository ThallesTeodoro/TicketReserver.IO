# Tasks

## 1. Solution Scaffolding & Project Setup

- [x] 1.1 Scaffold `TR.HW.Notification` solution (`.sln` and `.slnx`) with 5 projects (`Domain`, `Application`, `Infrastructure`, `Worker`, `UnitTests`), configure layer references and NuGet packages (`StackExchange.Redis`, `Polly`, `Confluent.Kafka`, `FluentValidation`, `xUnit`, `Moq`, `FluentAssertions`), and verify `dotnet build` succeeds with zero warnings.

## 2. Domain Layer Implementation

- [x] 2.1 Implement `CustomerNotification` aggregate root, enums (`NotificationType`, `NotificationChannel`, `NotificationStatus`), and `DomainException` in `TR.HW.Notification.Domain` enforcing non-anemic creation invariants and state transitions (`MarkAsSent`, `MarkAsFailed`).
- [x] 2.2 Implement unit tests in `TR.HW.Notification.UnitTests/Domain` verifying invariant enforcement, state transitions, and idempotency guards, and confirm `dotnet test` passes.

## 3. Application Layer (CQRS Lite & Use Cases)

- [x] 3.1 Implement CQRS Lite interfaces (`ICommand`, `ICommandHandler`), `SendCustomerNotificationCommand`, `SendCustomerNotificationCommandHandler`, and `SendCustomerNotificationCommandValidator` in `TR.HW.Notification.Application`.
- [x] 3.2 Implement static mapping extension methods (`CustomerNotificationMappingExtensions`, zero AutoMapper) and port interfaces (`INotificationInboxStore`, `INotificationDispatcher`).
- [x] 3.3 Implement unit tests in `TR.HW.Notification.UnitTests/Application` verifying multi-channel dispatch (`Email` + `Push`) for ticket issuance, payment refund, and payment decline outcomes, as well as validation failure handling, and confirm `dotnet test` passes.

## 4. Infrastructure Layer (Redis Distributed Inbox & Polly Resilient Dispatcher)

- [x] 4.1 Implement `RedisNotificationInboxStore` using `StackExchange.Redis` (`notif:inbox:{consumerGroup}:{messageId}` with `SET NX EX` TTL) in `TR.HW.Notification.Infrastructure`.
- [x] 4.2 Implement `INotificationProviderGateway`, `SimulatedNotificationProviderGateway`, and `ResilientNotificationDispatcher` using Polly (Exponential Backoff Retry + Circuit Breaker) in `TR.HW.Notification.Infrastructure`.
- [x] 4.3 Implement unit tests in `TR.HW.Notification.UnitTests/Infrastructure` verifying Redis inbox key formatting/deduplication and Polly transient retry recovery, and confirm `dotnet test` passes.

## 5. Worker Service & Kafka Integration

- [x] 5.1 Implement `NotificationWorker` (`BackgroundService`) and `KafkaConsumer` subscribing to `ticketing.v1.issued`, `payment.v1.refunded`, and `payment.v1.declined` under consumer group `cg-notification-worker`, checking Redis inbox deduplication, executing the command handler, routing poison pills to `.DLQ`, and committing Kafka offsets manually.
- [x] 5.2 Configure `DependencyInjection.cs`, `Program.cs`, and `appsettings.json` with fail-fast options validation throwing `ArgumentNullException` on missing parameters.
- [x] 5.3 Implement unit tests in `TR.HW.Notification.UnitTests/Workers` and `Configuration` verifying multi-topic message parsing, inbox duplicate skipping, DLQ routing on poisoned payloads, and fail-fast configuration validation, and confirm `dotnet test` passes.

## 6. Stack Integration & Verification

- [x] 6.1 Create `Dockerfile` and `docker-compose.yml` for `TR.HW.Notification` and register `./TR.HW.Notification/docker-compose.yml` in root `docker-compose.yml`.
- [x] 6.2 Execute `dotnet test` across all solution test suites in the repository and validate OpenSpec change artifacts with `openspec validate setup-notification-worker`.
