# Proposal: Setup Events Database Infrastructure & Schema

## Why

The `TR.API.Events` service (`SVC_EVENTOS`) is the authoritative source for venues, seating sections, events, and ticket tiers across the TicketReserver.IO platform. To begin isolated development of the Events API and validate domain persistence, a localized PostgreSQL 16 environment is required. Provisioning this database container with logical WAL logging (`wal_level = logical`) and pre-configuring the schema and initial tables enables immediate development while ensuring seamless future CDC integration with Debezium and Kafka.

## What Changes

- Provision a root or component-level `docker-compose.yml` declaring the `postgres` service dedicated to the Events microservice (`catalog_events_db`).
- Configure PostgreSQL 16 with logical decoding parameters (`wal_level=logical`, `max_wal_senders=10`, `max_replication_slots=10`) and proper healthchecks.
- Create automated database initialization and migration scripts (`init-events-db.sql`) that install the `uuid-ossp` extension and create the required DDL tables (`venues`, `seat_sections`, `events`, `ticket_tiers`, `seats`) as defined in `.agents/specification/db/catalog-events.md`.
- Provide environment configuration files (`.env.example` / `.env`) to manage database credentials and ports deterministically.
- Verify container startup, healthy status, and schema creation.

## Capabilities

### New Capabilities

- `catalog-events-db`: Provisioning, containerization, CDC-ready configuration, and schema definition for the Event Catalog PostgreSQL relational store.

### Modified Capabilities

<!-- No existing capabilities to modify -->

## Impact

- **Infrastructure**: Adds `docker-compose.yml`, database init scripts, and `.env` configuration.
- **Database**: Establishes `catalog_events_db` with relational constraints, UUID generation, foreign keys, and indexes for venues, sections, events, ticket tiers, and seats.
- **Downstream Services**: Sets up the persistence foundation required for the `TR.API.Events` WebApi and future Debezium CDC pipeline.
