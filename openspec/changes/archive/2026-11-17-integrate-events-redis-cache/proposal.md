# Proposal: Integrate Redis Modular Compose and L1 Cache in TR.API.Events

## Why

High-concurrency ticket sales generate massive read traffic on event catalog details (`GET /events/{id}`) and venue listings (`GET /venues`). Integrating a containerized Redis store with strict `noeviction` and AOF guarantees, paired with an L1 Cache-Aside and automatic cache invalidation strategy in `TR.API.Events`, shields the PostgreSQL database from read contention while establishing the shared Redis infrastructure required for future distributed seat holds.

## What Changes

- **Modular Redis Docker Compose (`docker/redis/docker-compose.yml`)**:
  - Provision Redis (`redis:7-alpine`) configured per `infra-spec.md` with `--maxmemory 512mb`, `--maxmemory-policy noeviction`, `--appendonly yes`, and `--appendfsync everysec`.
  - Include `./docker/redis/docker-compose.yml` in the root `docker-compose.yml` and wire `TR.API.Events/docker-compose.yml` with the Redis connection string and `depends_on: redis: condition: service_healthy`.
- **Redis Caching Abstraction & Implementation (`TR.API.Events`)**:
  - Add `ICacheService` contract in `TR.API.Events.Domain/Contracts/Caching/`.
  - Implement `RedisCacheService` using `StackExchange.Redis` in `TR.API.Events.Infrastructure/Caching/`.
- **L1 Cache-Aside & Automatic Invalidation (`TR.API.Events.Application`)**:
  - Cache `GetEventByIdQuery` results under key `catalog:event:{id}` (TTL: 1 hour) and `ListVenuesQuery` under `catalog:venues` (TTL: 1 hour).
  - Automatically invalidate `catalog:event:{id}` upon `UpdateEventCommand`, `ChangeEventStatusCommand`, and `CancelEventCommand`.
- **Readiness Probe Enhancement (`TR.API.Events.WebApi`)**:
  - Extend `/health/ready` to verify both PostgreSQL and Redis connectivity.

## Capabilities

### New Capabilities
- `infra-redis-store`: Containerized Redis service configured with `noeviction` policy, AOF persistence, healthcheck, and modular Docker Compose integration.

### Modified Capabilities
- `catalog-events-api`: Add L1 Redis Cache-Aside for event and venue reads, deterministic cache invalidation on event mutations, and Redis readiness reporting.

## Impact

- **Infrastructure (`docker/redis/docker-compose.yml`, `docker-compose.yml`, `TR.API.Events/docker-compose.yml`)**: Adds `ticketreserver-redis` container on port `6379` attached to `ticketreserver-internal-net`.
- **Dependencies (`TR.API.Events.Infrastructure`)**: Adds NuGet package `StackExchange.Redis`.
- **Application & Domain (`TR.API.Events`)**: Updates query and command handlers in `Events` and `Venues` slices to use `ICacheService`, covered by unit tests.
