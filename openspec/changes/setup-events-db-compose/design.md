# Design: Events Database Setup and Containerization

## Context

See `proposal.md` for motivation. The architectural specification ([infra-spec.md](file:///home/thallesteodoro/development/ticket-reservation-pdi/.agents/specification/infra/infra-spec.md)) dictates a segregated logical database `catalog_events_db` operating on PostgreSQL 16 with logical decoding enabled (`wal_level = logical`) to support future CDC via Debezium. The relational data schema is defined in [catalog-events.md](file:///home/thallesteodoro/development/ticket-reservation-pdi/.agents/specification/db/catalog-events.md).

## Goals / Non-Goals

**Goals:**
- Provide a reproducible Docker Compose environment with PostgreSQL 16 dedicated to the Event Catalog service.
- Enable `wal_level=logical` from day one for seamless CDC compatibility.
- Automate initial schema creation (tables, foreign keys, unique constraints, and `uuid-ossp`) via an initialization script mounted into `/docker-entrypoint-initdb.d/`.
- Provide an `.env.example` file and configure healthchecks for resilient container lifecycle management.

**Non-Goals:**
- Setting up the .NET 8 WebApi container (deferred to the component setup change).
- Provisioning Kafka, Debezium, Redis, or Elasticsearch (deferred to downstream infrastructure scaling phases).
- Setting up runtime EF Core migrations (the DDL script provisions the base tables; EF Core will map to these definitions).

## Decisions

### 1. PostgreSQL 16 Alpine Image
- **Choice**: Use `postgres:16-alpine`.
- **Rationale**: Minimal footprint, fast boot times, standard official PostgreSQL distribution.
- **Alternatives considered**: `debezium/postgres:16` (pre-configured for CDC, but less flexible and larger than official Alpine image).

### 2. Runtime Flags for WAL Configuration
- **Choice**: Pass `-c wal_level=logical -c max_wal_senders=10 -c max_replication_slots=10` via `command` in `docker-compose.yml`.
- **Rationale**: Keeps configuration declarative in Docker Compose without requiring a mounted custom `postgresql.conf` file.
- **Alternatives considered**: Mounting custom `postgresql.conf` (adds extra file management complexity).

### 3. Native Docker Entrypoint Init Script
- **Choice**: Place the DDL script in `docker/postgres/init-events-db.sql` and mount it to `/docker-entrypoint-initdb.d/init-events-db.sql`.
- **Rationale**: The official PostgreSQL container automatically executes `.sql` scripts in this folder when the database volume is first initialized.
- **Alternatives considered**: Running manual `psql` commands via CLI (less repeatable for developers).

### 4. Healthcheck via `pg_isready`
- **Choice**: Include a Docker healthcheck using `pg_isready -U ${POSTGRES_USER:-postgres} -d ${POSTGRES_DB:-catalog_events_db}`.
- **Rationale**: Allows dependent services (such as future API containers or test runners) to use `condition: service_healthy`.

## Risks / Trade-offs

- **[Risk] Existing Volume Bypass**: Docker init scripts only execute if the data directory is empty. If a developer already has a volume from a previous run, changes to `init-events-db.sql` will not apply.
  → **Mitigation**: Document commands clearly in tasks and README (`docker compose down -v` to reset state).
- **[Risk] Port 5432 Host Collision**: Developers might already run a local PostgreSQL instance on port 5432.
  → **Mitigation**: Expose the port using `${EVENTS_DB_PORT:-5432}:5432` in `docker-compose.yml` so it can be remapped easily via `.env`.
