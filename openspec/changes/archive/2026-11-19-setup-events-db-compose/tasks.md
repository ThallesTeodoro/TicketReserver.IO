# Tasks: Setup Events Database and Docker Compose

## 1. Environment Configuration & Docker Compose Setup

- [x] 1.1 Create `.env.example` and `.env` with PostgreSQL variables (`POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB=catalog_events_db`, `EVENTS_DB_PORT=5432`) and verify variables are present and correct.
- [x] 1.2 Create `docker-compose.yml` declaring the `postgres` service with `postgres:16-alpine`, CDC flags (`-c wal_level=logical -c max_wal_senders=10 -c max_replication_slots=10`), healthcheck via `pg_isready`, persistent volume, and network `ticketreserver-internal-net`, and verify configuration using `docker compose config`.

## 2. Database Schema Initialization Script

- [x] 2.1 Create `docker/postgres/init-events-db.sql` with `CREATE EXTENSION IF NOT EXISTS "uuid-ossp"`, and DDL definitions for `venues`, `seat_sections`, `events`, `ticket_tiers`, and `seats` with foreign keys, checks, and unique indexes according to `.agents/specification/db/catalog-events.md`, and verify SQL script completeness.

## 3. Container Execution & Schema Validation

- [x] 3.1 Start the container stack with `docker compose up -d` and verify container health status (`docker compose ps`).
- [x] 3.2 Query the database using `docker compose exec postgres psql -U postgres -d catalog_events_db -c "SHOW wal_level;"` and verify output is `logical`.
- [x] 3.3 Query the schema using `docker compose exec postgres psql -U postgres -d catalog_events_db -c "\dt"` and verify all 5 tables are listed.
