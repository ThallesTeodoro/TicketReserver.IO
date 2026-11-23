# Design

## Context

See `proposal.md` for motivation. The search data plane is established: ElasticSearch 8.x is configured in `docker/elasticsearch/` with index `events_search_v1` and alias `events_search`, populated via CDC streams by `TR.HW.Repl`. This microservice `TR.API.Search` provides the public read-only query entry point for clients and downstream services requiring fast full-text search, autocomplete, and faceted filtering without placing query load on the PostgreSQL catalog database.

## Goals / Non-Goals

**Goals:**
- Implement a dedicated read-only .NET 10 microservice `TR.API.Search` following Clean Architecture and CQRS Lite Native across 5 projects (`Domain`, `Application`, `Infrastructure`, `WebApi`, `UnitTests`).
- Expose search endpoints via Carter:
  - `GET /api/v1/search/events`: Full-text multi-match search with field boosts (`title^3`, `category^2`, `tags^1.5`, `description`), filters (`category`, `status`, `from_date`, `to_date`, `min_price`, `max_price`, `geo_distance`), sort options (`relevance`, `price_asc`, `price_desc`, `date_asc`, `distance`), pagination (`page`, `page_size`), and facet counts (`categories`, `cities`).
  - `GET /api/v1/search/events/autocomplete`: Sub-millisecond prefix suggestion queries querying the Edge N-Gram `title.autocomplete` subfield.
  - `GET /api/v1/search/events/{id}`: Single event search document lookup by identifier.
  - `GET /api/v1/search/health`: Cluster health check returning HTTP 200/503.
- Use `Elastic.Clients.Elasticsearch` (v8) with resilient Polly retry policies for transient fault tolerance.
- Standardize all API responses using `JsonResponse<TData, TError>`.

**Non-Goals:**
- Mutating or indexing data into ElasticSearch (writes are strictly owned by `TR.HW.Repl` via Debezium CDC).
- Direct connection to PostgreSQL or Redis (search queries are 100% satisfied by ElasticSearch).
- In-process auth token verification (delegated to API Gateway / Ingress).

## Decisions

### 1. CQRS Lite Native and Zero External Frameworks (MediatR/AutoMapper)
- **Rationale**: Project standard dictates zero runtime reflection bloat. We use simple interfaces (`IQuery<TResponse>`, `IQueryHandler<TQuery, TResponse>`), manual DI registration, and static extension methods (`EventSearchDtoMappingExtensions`) for DTO conversions.
- **Alternatives Considered**: MediatR, Mapster, AutoMapper — rejected per project guidelines.

### 2. Query Context vs Filter Context in ElasticSearch
- **Rationale**: Full-text queries on `q` execute in the **must / should** query context to calculate relevance `_score` with field boosting (`title^3`, `category^2`, `tags^1.5`). Categorical and range filters (`category`, `status`, `price`, `dates`, `geo_distance`) execute strictly inside the **filter** context. This enables ElasticSearch to cache filter bitsets and skip scoring overhead for filters.
- **Alternatives Considered**: Combining all criteria in `must` — rejected due to score distortion and lack of bitset caching.

### 3. Edge N-Gram Autocomplete Strategy
- **Rationale**: Querying `title.autocomplete` with `term` or `match` query leveraging the `autocomplete_analyzer` built into `events_search_v1` avoids slow regex/wildcard searches.
- **Alternatives Considered**: ElasticSearch completion suggester — rejected as Edge N-Gram supports infix and mid-token matching with the existing index mapping.

### 4. Carter Minimal APIs with Standardized Envelopes
- **Rationale**: Minimal APIs provide maximum throughput and lower memory allocation. Carter modules (`SearchEventsModule`, `HealthModule`) organize endpoints cleanly into vertical slices. Every response wraps results in `JsonResponse<TData, TError>` with appropriate HTTP status codes.
- **Alternatives Considered**: Traditional ASP.NET Core MVC Controllers — rejected due to higher overhead.

## Risks / Trade-offs

- **[Risk: ElasticSearch Cluster Outage]** → **Mitigation**: Queries are wrapped with Polly retry policies with exponential backoff and timeout. The `/api/v1/search/health` endpoint reports cluster unavailability to the load balancer / Ingress.
- **[Risk: CDC Sync Lag]** → **Mitigation**: Debezium streaming directly from PostgreSQL WAL achieves sub-second propagation (~100-300ms). The frontend handles eventual consistency by confirming reservations and bookings against the primary relational transactional stores.

## Migration Plan

- Solução `TR.API.Search` é adicionada ao repositório com build e testes unitários.
- Imagem Docker e serviço registrados no `docker-compose.yml` raiz.
- Não há migração de banco de dados necessária (o índice ElasticSearch já está provisionado e populado por `TR.HW.Repl`).
