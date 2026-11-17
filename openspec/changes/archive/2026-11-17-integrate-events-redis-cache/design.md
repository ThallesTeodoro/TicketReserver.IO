# Design: Integrate Redis Modular Compose and L1 Cache in TR.API.Events

## Context

See [proposal.md](file:///home/thallesteodoro/development/ticket-reservation-pdi/openspec/changes/integrate-events-redis-cache/proposal.md) for motivation, [catalog-events.md](file:///home/thallesteodoro/development/ticket-reservation-pdi/.agents/specification/db/catalog-events.md) (Section 5) for the Redis key schema, and [infra-spec.md](file:///home/thallesteodoro/development/ticket-reservation-pdi/.agents/specification/infra/infra-spec.md) (Section 3.2) for Redis infrastructure constraints.

## Goals / Non-Goals

**Goals:**
- Provision a modular Docker Compose file (`docker/redis/docker-compose.yml`) for Redis 7 configured with `maxmemory 512mb`, `maxmemory-policy noeviction`, and AOF (`appendonly yes`), included in the root `docker-compose.yml`.
- Introduce a clean, resilient `ICacheService` contract in `TR.API.Events.Domain` and implement it via `StackExchange.Redis` in `TR.API.Events.Infrastructure`.
- Implement L1 Cache-Aside in `GetEventByIdQueryHandler` (`catalog:event:{id}`, TTL 1h) and `ListVenuesQueryHandler` (`catalog:venues`, TTL 1h).
- Implement automatic cache invalidation of `catalog:event:{id}` in `UpdateEventCommandHandler`, `ChangeEventStatusCommandHandler`, and `CancelEventCommandHandler`.
- Extend `/health/ready` to verify both PostgreSQL and Redis connectivity.

**Non-Goals:**
- Distributed seat holds (`SET seat:{id} NX EX 600`) — this belongs to `TR.API.Reservation` in a future change, though it will reuse this exact `ticketreserver-redis` container.

## Decisions

### Decision 1: Modular Docker Compose for Redis (`docker/redis/docker-compose.yml`)
- **Decision:** Place the Redis service definition in `docker/redis/docker-compose.yml` and include it in the root `docker-compose.yml` via `include: [ ./docker/redis/docker-compose.yml ]`.
- **Rationale:** Matches our modular compose architecture where shared infrastructure lives under `docker/<service>/docker-compose.yml` and microservices live under `TR.<Type>.<Name>/docker-compose.yml`.

### Decision 2: `ICacheService` Abstraction in Domain Layer
- **Decision:** Define `ICacheService` (`GetAsync<T>`, `SetAsync<T>`, `RemoveAsync`) in `TR.API.Events.Domain/Contracts/Caching/ICacheService.cs` with zero external package dependencies, and implement `RedisCacheService` in `TR.API.Events.Infrastructure/Caching/RedisCacheService.cs` using `IConnectionMultiplexer` (`StackExchange.Redis`).
- **Rationale:** Preserves Clean Architecture boundaries so Application handlers remain 100% unit-testable with `Moq<ICacheService>`.

### Decision 3: Fail-Open Cache Resilience
- **Decision:** Wrap Redis read/write/delete operations in `RedisCacheService` with try/catch logging warnings on transient Redis errors so a cache hiccup falls back to PostgreSQL rather than failing catalog read/write requests.
- **Rationale:** For L1 catalog caching, availability of the catalog takes precedence over cache hits; `/health/ready` still reports degraded/unready status if Redis is unreachable.

## Risks / Trade-offs

- **[Risk] Stale Cache After Event Mutation:** If an event is updated or transitions status, stale data could be served for up to 1 hour.
  - *Mitigation:* Explicitly call `await cacheService.RemoveAsync($"catalog:event:{command.Id}", cancellationToken)` in `UpdateEventCommandHandler`, `ChangeEventStatusCommandHandler`, and `CancelEventCommandHandler` immediately after `unitOfWork.SaveChangesAsync`.
