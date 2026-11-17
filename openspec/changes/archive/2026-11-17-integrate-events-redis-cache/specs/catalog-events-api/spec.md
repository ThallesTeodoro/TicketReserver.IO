# Spec Delta: catalog-events-api

## ADDED Requirements

### Requirement: L1 Redis Cache-Aside for Catalog Queries
The system SHALL cache event detail responses (`GET /events/{id}`) under `catalog:event:{id}` (TTL 1 hour) and venue listings (`GET /venues`) under `catalog:venues` (TTL 1 hour) in Redis.

#### Scenario: Cache miss populates Redis from PostgreSQL
- **WHEN** `GET /events/{id}` is requested and `catalog:event:{id}` is not present in Redis
- **THEN** the API fetches the event from PostgreSQL, stores the serialized representation in Redis under `catalog:event:{id}` with a 1-hour TTL, and returns HTTP 200.

#### Scenario: Cache hit serves response directly from Redis
- **WHEN** `GET /events/{id}` is requested and `catalog:event:{id}` is present in Redis
- **THEN** the API returns the cached event with HTTP 200 without querying PostgreSQL.

### Requirement: Automatic Cache Invalidation on Event Mutations
The system SHALL invalidate the `catalog:event:{id}` cache entry whenever an event is updated (`PUT /events/{id}`), transitions status (`PATCH /events/{id}/status`), or is logically cancelled (`DELETE /events/{id}`).

#### Scenario: Cache invalidated after event update or status change
- **WHEN** a client executes a successful `PUT /events/{id}`, `PATCH /events/{id}/status`, or `DELETE /events/{id}`
- **THEN** the API removes `catalog:event:{id}` from Redis so subsequent `GET /events/{id}` requests reflect the updated state immediately.
