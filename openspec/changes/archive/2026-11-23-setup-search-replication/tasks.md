# Tasks

## 1. Infrastructure Setup (ElasticSearch & Debezium CDC)

- [x] 1.1 Create ElasticSearch 8.x service configuration in `docker/elasticsearch/docker-compose.yml` with initialization script creating index `events_search_v1` (with `pt_text_analyzer` and `autocomplete_analyzer` Edge N-Gram) and alias `events_search`.
- [x] 1.2 Create Debezium Kafka Connect service configuration in `docker/debezium/docker-compose.yml` with connector registration payload for PostgreSQL `catalog_events_db`.

## 2. Solution Scaffolding & Project Setup

- [x] 2.1 Scaffold `TR.HW.Repl` solution (`.sln` and `.slnx`) with 5 projects (`Domain`, `Application`, `Infrastructure`, `Worker`, `UnitTests`), configure layer references and NuGet packages (`Elastic.Clients.Elasticsearch`, `Confluent.Kafka`, `Polly`, `FluentValidation`, `xUnit`, `Moq`, `FluentAssertions`), and verify `dotnet build` succeeds.

## 3. Domain Layer Implementation

- [x] 3.1 Implement `EventSearchDocument` aggregate, `VenueSearchInfo`, `PricingSummary`, `TicketTierSummary`, and CDC change model entities in `TR.HW.Repl.Domain` enforcing non-anemic invariants and domain validation.
- [x] 3.2 Implement unit tests in `TR.HW.Repl.UnitTests/Domain` verifying document creation, price recalculation invariants, and tier summary updates, and confirm `dotnet test` passes.

## 4. Application Layer (CQRS Lite & CDC Use Cases)

- [x] 4.1 Implement CQRS Lite interfaces (`ICommand`, `ICommandHandler`), `ReplicateEventChangeCommand`, `ReplicateTicketTierChangeCommand`, handlers, and FluentValidation validators in `TR.HW.Repl.Application`.
- [x] 4.2 Implement static mapping extension methods (`CdcMappingExtensions`, zero AutoMapper) and port interface `IEventSearchRepository`.
- [x] 4.3 Implement unit tests in `TR.HW.Repl.UnitTests/Application` verifying event upsert handling, partial tier pricing updates, cancellation status updates, and deletion handling, and confirm `dotnet test` passes.

## 5. Infrastructure Layer (ElasticSearch Client & Resilient Repository)

- [x] 5.1 Implement `ElasticsearchEventRepository` in `TR.HW.Repl.Infrastructure` using `Elastic.Clients.Elasticsearch` (v8) with Polly exponential retry for indexing, document upsert, partial pricing updates, and deletion.
- [x] 5.2 Implement unit tests in `TR.HW.Repl.UnitTests/Infrastructure` verifying ElasticSearch request creation, error handling, and Polly retry policies, and confirm `dotnet test` passes.

## 6. Worker Service & Kafka Integration

- [x] 6.1 Implement `SearchReplicationWorker` (`BackgroundService`) and `KafkaConsumer` subscribing to `catalog.events.events`, `catalog.events.venues`, and `catalog.events.ticket_tiers` under consumer group `cg-search-replication-worker`, dispatching replication commands, routing poison pills to `.DLQ`, and committing offsets manually.
- [x] 6.2 Configure `DependencyInjection.cs`, `Program.cs`, and `appsettings.json` with fail-fast options validation throwing `ArgumentNullException` on missing parameters.
- [x] 6.3 Implement unit tests in `TR.HW.Repl.UnitTests/Workers` and `Configuration` verifying multi-topic CDC payload parsing, DLQ routing on malformed payloads, and configuration validation, and confirm `dotnet test` passes.

## 7. Stack Integration & Repository Verification

- [x] 7.1 Create `Dockerfile` and `docker-compose.yml` for `TR.HW.Repl` and register `./docker/elasticsearch/docker-compose.yml`, `./docker/debezium/docker-compose.yml`, and `./TR.HW.Repl/docker-compose.yml` in root `docker-compose.yml`.
- [x] 7.2 Execute `dotnet test` across all solution test suites in the repository and validate OpenSpec change artifacts with `openspec validate setup-search-replication`.
