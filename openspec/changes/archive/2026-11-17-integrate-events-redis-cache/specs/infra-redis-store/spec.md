# Spec Delta: infra-redis-store

## Purpose

Provides containerized in-memory storage and distributed locking infrastructure using Redis with strict `noeviction` memory guarantees and AOF durability for the TicketReserver.IO platform.

## ADDED Requirements

### Requirement: Redis Container Provisioning with NoEviction and AOF
The system SHALL provide a containerized Redis service configured with `maxmemory 512mb`, `maxmemory-policy noeviction`, and AOF persistence (`appendonly yes`, `appendfsync everysec`) orchestrated via modular Docker Compose.

#### Scenario: Redis container starts healthy via root compose include
- **WHEN** the container stack is started via root `docker compose up -d`
- **THEN** the `ticketreserver-redis` container transitions to a healthy state on `ticketreserver-internal-net` and responds `PONG` to `redis-cli ping`.

#### Scenario: NoEviction and AOF policies are enforced
- **WHEN** the Redis server configuration is inspected at runtime
- **THEN** `maxmemory-policy` is set to `noeviction` and `appendonly` is set to `yes`.
