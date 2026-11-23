# Proposal

## Why

Currently, event discovery relies directly on relational PostgreSQL queries within the catalog domain, which lacks full-text relevance scoring, fuzzy matching, autocomplete suggestions, geo-distance filtering, and facet aggregations under high query concurrency. To fulfill the search domain design (`SVC_BUSCA` in `arch-decisions.md`) and leverage the CDC denormalization pipeline implemented in `TR.HW.Repl`, we need a high-performance, read-only search service (`TR.API.Search`) built on ASP.NET Core 10 Minimal APIs, Carter, and ElasticSearch 8.x.

## What Changes

- Scaffold a new microservice solution `TR.API.Search` (.NET 10) organized with Clean Architecture and CQRS Lite Native across 5 projects (`Domain`, `Application`, `Infrastructure`, `WebApi`, and `UnitTests`).
- Implement the `event-search-api` capability providing full-text search, autocomplete, and event document retrieval from ElasticSearch:
  - `GET /api/v1/search/events`: Full-text multi-match search with field boosting (`title^3`, `category^2`, `tags^1.5`, `description`), faceted filtering (category, status, date range, price range, geo-distance bounding), sorting (relevance, price, date, distance), and pagination.
  - `GET /api/v1/search/events/autocomplete`: High-speed prefix suggestions powered by the ElasticSearch `autocomplete_analyzer` Edge N-Gram index.
  - `GET /api/v1/search/events/{id}`: Single event search document lookup by identifier.
  - `GET /api/v1/search/health`: Healthcheck endpoint verifying connectivity to ElasticSearch.
- Implement resilient search query execution via `Elastic.Clients.Elasticsearch` (v8) wrapped in Polly retry policies.
- Standardize HTTP responses with `JsonResponse<TData, TError>` and Carter Minimal API modules.
- Create containerization artifacts (`Dockerfile`, `docker-compose.yml`) and register `TR.API.Search` in the root `docker-compose.yml`.

## Capabilities

### New Capabilities
- `event-search-api`: Read-only HTTP search API for event discovery, full-text multi-match queries with field boosting, category/city facet aggregations, geo-distance filtering, edge n-gram autocomplete, and single document lookup over ElasticSearch 8.x.

### Modified Capabilities
<!-- None -->

## Impact

- **Affected Systems**: `SVC_BUSCA` domain in `TicketReserver.IO` architecture.
- **Dependencies**: Uses `Elastic.Clients.Elasticsearch` (v8.17.1), `Carter` (9.0.0), `Polly` (8.5.2), `FluentValidation` (12.1.0), and Serilog.
- **APIs**: Exposes `/api/v1/search/events`, `/api/v1/search/events/autocomplete`, and `/api/v1/search/events/{id}`.
- **Infrastructure**: Connects to ElasticSearch (`http://elasticsearch:9200`) querying alias `events_search`.
