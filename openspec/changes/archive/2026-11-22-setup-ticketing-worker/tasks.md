# Tasks

## 1. Database Initialization & Solution Scaffolding

- [x] 1.1 Create PostgreSQL init script `docker/postgres/init-ticketing-db.sql` with tables `tickets`, `seat_allocations`, `ticket_checkins`, `outbox_messages`, `inbox_messages`, enums, and unique constraint on `(event_id, seat_id)`.
- [x] 1.2 Register PostgreSQL init script mount in root `docker-compose.yml` and add `ticketing.v1.issued` and `ticketing.v1.failed` topics to `docker/kafka/init-kafka-topics.sh`.
- [x] 1.3 Scaffold `TR.HW.Ticketing` solution (`.sln` and `.slnx`) with 5 projects (`Domain`, `Application`, `Infrastructure`, `Worker`, `UnitTests`), configure layer references, and verify `dotnet build` succeeds.

## 2. Domain Layer Implementation

- [x] 2.1 Implement `Ticket` aggregate root, `SeatAllocation` entity, `TicketCheckin` entity, domain events (`TicketIssuedDomainEvent`, `TicketingFailedDomainEvent`), enums, and `IQrCodeGenerator` interface in `TR.HW.Ticketing.Domain`.
- [x] 2.2 Implement unit tests in `TR.HW.Ticketing.UnitTests/Domain` verifying invariant rules (positive price, status transitions, checkin rules, collection encapsulation).

## 3. Application Layer (CQRS Lite & Use Cases)

- [x] 3.1 Implement CQRS Lite interfaces (`ICommand`, `ICommandHandler`), `IssueTicketsCommand`, `IssueTicketsCommandHandler`, and `IssueTicketsCommandValidator` in `TR.HW.Ticketing.Application`.
- [x] 3.2 Implement domain/DTO static mapping extension methods (zero AutoMapper) and port interfaces (`ITicketingRepository`, `IKafkaTicketingProducer`).
- [x] 3.3 Implement unit tests in `TR.HW.Ticketing.UnitTests/Application` verifying ticket issuance orchestration and failure event emission on seat collision.

## 4. Infrastructure Layer (Persistence, Cryptography & Kafka)

- [x] 4.1 Implement `HmacSha256QrCodeGenerator` in `TR.HW.Ticketing.Infrastructure/Cryptography` with HMAC-SHA256 signature and URL-safe Base64 token generation.
- [x] 4.2 Implement `TicketingRepository` using Dapper and Npgsql with transaction isolation, handling PostgreSQL exception `23505 (unique_violation)` to persist `ticketing.v1.failed` compensation event.
- [x] 4.3 Implement `KafkaTicketingProducer` in `TR.HW.Ticketing.Infrastructure/Messaging` with W3C `traceparent` headers, topic resolution (`ticketing.v1.issued` / `ticketing.v1.failed`), and DLQ fallback.
- [x] 4.4 Implement unit tests in `TR.HW.Ticketing.UnitTests/Infrastructure` verifying HMAC generation and repository error handling.

## 5. Worker Service & Integration

- [x] 5.1 Implement `TicketIssuanceWorker` (`BackgroundService`) consuming `payment.v1.approved`, checking inbox deduplication, executing command handler, and committing Kafka offset.
- [x] 5.2 Configure `DependencyInjection.cs`, `Program.cs`, and `appsettings.json` with fail-fast options validation throwing `ArgumentNullException` on missing parameters.
- [x] 5.3 Implement comprehensive unit tests in `TR.HW.Ticketing.UnitTests/Workers` verifying consumption loop, error handling, DLQ routing, and offset acknowledgment.

## 6. Stack Integration & Verification

- [x] 6.1 Create `Dockerfile` and `docker-compose.yml` for `TR.HW.Ticketing` and register `tr-hw-ticketing` in root `docker-compose.yml`.
- [x] 6.2 Execute `dotnet test` across all solution test suites in the repository, validate OpenSpec specs with `openspec validate --specs`, and verify zero warnings.
