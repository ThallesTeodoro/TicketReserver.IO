# Design

## Context

The **TicketReserver.IO** search specification (`search-db.md` and `infra-spec.md`) defines a physical CQRS architecture. In `SVC_EVENTOS`, changes to PostgreSQL `catalog_events_db` are captured by Debezium CDC and streamed to Apache Kafka topics (`catalog.events.events`, `catalog.events.venues`, `catalog.events.ticket_tiers`). `TR.HW.Repl` is the background replication worker that processes these CDC streams, denormalizes the relational entities into `EventSearchDocument`, and maintains the ElasticSearch `events_search` index with sub-second consistency. See `proposal.md` and `specs/search-replication-worker/spec.md`.

## Goals / Non-Goals

**Goals:**
- Provision ElasticSearch 8.x in Docker Compose with preconfigured index `events_search_v1` (Portuguese stemmer, asciifolding, edge n-gram autocomplete analyzer) and zero-downtime alias `events_search`.
- Provision Debezium Kafka Connect with PostgreSQL connector configuration for `catalog_events_db`.
- Subscribe to CDC topics `catalog.events.events`, `catalog.events.venues`, and `catalog.events.ticket_tiers` under consumer group `cg-search-replication-worker` (`EnableAutoCommit = false`).
- Model `EventSearchDocument`, `VenueSearchInfo`, `PricingSummary`, and `TicketTierSummary` as rich domain objects in `TR.HW.Repl.Domain`.
- Implement `ElasticsearchEventRepository` using official `Elastic.Clients.Elasticsearch` (v8) with Polly exponential retry for resilient index operations.
- Handle Debezium CDC envelopes (`before`, `after`, `op`: `c`, `u`, `d`, `r`) gracefully, updating event documents, executing partial tier updates, and handling cancellations.
- Maintain Clean Architecture (.NET 10) across 5 projects with native CQRS Lite (zero MediatR / zero AutoMapper).

**Non-Goals:**
- Serving HTTP queries to clients (owned by `TR.API.Search` in the next change).
- Directly querying `catalog_events_db` from `TR.HW.Repl` (worker consumes streaming CDC payloads).

## Decisions

### Decision 1: ElasticSearch 8.x Index Configuration and Analyzers
- **Choice**: Index `events_search_v1` configured with 2 shards and 1 replica (local dev), using:
  - `pt_text_analyzer`: `standard` tokenizer + `lowercase`, `asciifolding`, `pt_stop`, `pt_stemmer`.
  - `autocomplete_analyzer`: `standard` tokenizer + `lowercase`, `asciifolding`, `edge_ngram_filter` (min_gram: 2, max_gram: 15).
  - Managed via alias `events_search`.
- **Rationale**: Directly satisfies `search-db.md` Section 3, allowing instant full-text and prefix search in Portuguese without accent sensitivity.

### Decision 2: Official .NET 8 Client `Elastic.Clients.Elasticsearch` (v8)
- **Choice**: Use `Elastic.Clients.Elasticsearch` version 8.x instead of legacy `NEST` (v7).
- **Rationale**: Modern C# record support, improved memory allocation, native support for ElasticSearch 8 server features, and long-term compatibility.

### Decision 3: CDC Ingestion and Partial Updating Strategy
- **Choice**:
  - `catalog.events.events`: When `op == 'c'` or `op == 'u'`, upsert document. When `status == 'CANCELLED'`, update document status. When `op == 'd'`, delete document.
  - `catalog.events.ticket_tiers`: Perform partial document update updating `pricing.min_price`, `pricing.max_price`, and `tiers_summary`.
  - `catalog.events.venues`: Update nested `venue` object across affected event documents.
- **Rationale**: Partial updates avoid unnecessary full document reindexing when only ticket tier prices change.

### Decision 4: Resilient Kafka Offset Commit and DLQ Fallback
- **Choice**: Execute manual Kafka offset commit only after ElasticSearch acknowledges the index/update request. Malformed CDC payloads are routed to `<topic>.DLQ`.
- **Rationale**: Guarantees at-least-once delivery without stalling partition consumption on unparseable poison pill messages.

## Risks / Trade-offs

- **[Risk: ElasticSearch temporary cluster unavailability]** → Mitigation: Wrap index/update operations with a Polly retry policy with exponential backoff and jitter. If retries are exhausted, the worker does not commit the offset, allowing Kafka redelivery once the cluster recovers.
- **[Risk: Out-of-order CDC events (e.g. tier update arrives before event creation)]** → Mitigation: Use ElasticSearch `doc_as_upsert` or check document existence, logging a transient warning and deferring tier enrichment if the parent event document is not yet present.
