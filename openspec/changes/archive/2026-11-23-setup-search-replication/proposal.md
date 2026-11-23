# Proposal

## Why

To enable sub-15ms event discovery, fuzzy search, edge n-gram autocomplete, and geospatial filtering without placing analytical read load on the relational OLTP PostgreSQL database (`catalog_events_db`), the platform requires a physical CQRS architecture. This change introduces the ElasticSearch 8.x search cluster, Debezium CDC streaming from PostgreSQL to Apache Kafka, and the dedicated CDC replication worker (`TR.HW.Repl`) that denormalizes relational changes into aggregated search documents.

## What Changes

- Provision ElasticSearch 8.x service in Docker Compose (`docker/elasticsearch/docker-compose.yml`) with automated provisioning of index `events_search_v1` (including Portuguese stemmer, asciifolding, and edge n-gram autocomplete analyzers) and alias `events_search`.
- Provision Debezium Kafka Connect service (`docker/debezium/docker-compose.yml`) and connector configuration capturing WAL events from `catalog_events_db` into Kafka topics `catalog.events.*`.
- Scaffold `TR.HW.Repl` solution with clean separation across 5 projects (`Domain`, `Application`, `Infrastructure`, `Worker`, `UnitTests`) following .NET 10 and CQRS Lite guidelines (zero MediatR and zero AutoMapper).
- Model denormalized `EventSearchDocument` and CDC domain entities in `TR.HW.Repl.Domain`.
- Implement CQRS Lite commands and handlers in `TR.HW.Repl.Application` to process full event upserts, venue updates, partial ticket tier price updates, and event cancellations.
- Implement `ElasticsearchEventRepository` in `TR.HW.Repl.Infrastructure` using official `Elastic.Clients.Elasticsearch` (v8) with Polly retry policies for resilient indexing.
- Implement `SearchReplicationWorker` (`BackgroundService`) subscribing to CDC topics (`catalog.events.events`, `catalog.events.venues`, `catalog.events.ticket_tiers`) under consumer group `cg-search-replication-worker` with manual offset commits and DLQ routing.
- Configure multi-stage `Dockerfile` and `docker-compose.yml` for `TR.HW.Repl`, registering it in the root `docker-compose.yml`.

## Capabilities

### New Capabilities
- `search-replication-worker`: Consumes PostgreSQL CDC change streams from Apache Kafka, denormalizes event, venue, and pricing data into `EventSearchDocument`, and maintains the ElasticSearch search index with real-time upserts and partial updates.

### Modified Capabilities
<!-- None -->

## Impact

- **New Projects**: `TR.HW.Repl` (`Domain`, `Application`, `Infrastructure`, `Worker`, `UnitTests`).
- **Infrastructure**: ElasticSearch 8.x container (:9200) and Debezium Kafka Connect container (:8083) added to `ticketreserver-internal-net`.
- **Kafka**: Consumes CDC topics (`catalog.events.events`, `catalog.events.venues`, `catalog.events.ticket_tiers`) with `.DLQ` fallback.
- **Root Docker Compose**: Registration of ElasticSearch, Debezium, and `ticketreserver-search-replication-worker`.
