# catalog-events-db Specification

## Purpose

Provides containerized relational storage for the Event Catalog service (`SVC_EVENTOS`), supporting CDC replication and initial relational DDL schemas.

## Requirements

### Requirement: PostgreSQL 16 Container Provisioning
The system SHALL provide a containerized PostgreSQL 16 database dedicated to the Event Catalog service with logical replication enabled.

#### Scenario: Database container health check passes
- **WHEN** the docker compose stack is started via `docker compose up -d`
- **THEN** the PostgreSQL container transitions to a healthy state and accepts client connections on the configured port.

#### Scenario: Logical decoding is active
- **WHEN** the container initializes
- **THEN** PostgreSQL configuration sets `wal_level` to `logical`, allowing CDC consumers to connect.

### Requirement: Automatic Schema and Table Initialization
The system SHALL automatically initialize the `catalog_events_db` database and create the required domain tables, constraints, and extensions during container startup.

#### Scenario: Tables and extension verification
- **WHEN** the database container starts for the first time
- **THEN** the `uuid-ossp` extension is created and tables `venues`, `seat_sections`, `events`, `ticket_tiers`, and `seats` are provisioned according to the specification.

#### Scenario: Relational constraints enforcement
- **WHEN** a record violates a foreign key or unique constraint in `venues`, `seat_sections`, or `events`
- **THEN** the database rejects the insert or update operation with a constraint violation error.

### Requirement: Deterministic Environment Configuration
The system SHALL support externalized environment variables for database credentials, ports, and volume paths via a `.env` template.

#### Scenario: Custom credentials through environment variables
- **WHEN** custom user and password values are supplied in `.env`
- **THEN** the PostgreSQL container uses those credentials for authentication.

### Requirement: Deterministic Initial Venue and Section Seeding
The system SHALL seed default venues and seat sections idempotently so that event creation and catalog testing have valid relational targets out of the box.

#### Scenario: Default venues and sections are available after initialization
- **WHEN** the database initializes or the Event Catalog API starts up
- **THEN** at least two default venues and their seat sections exist in `venues` and `seat_sections` without duplicating records on subsequent restarts.
