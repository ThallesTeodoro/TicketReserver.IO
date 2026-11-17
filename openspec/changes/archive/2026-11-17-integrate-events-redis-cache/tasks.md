# Tasks: Integrate Redis Modular Compose and L1 Cache in TR.API.Events

## 1. Modular Redis Docker Compose

- [x] 1.1 Create `docker/redis/docker-compose.yml` defining the `redis` service (`redis:7-alpine`) with `--maxmemory 512mb --maxmemory-policy noeviction --appendonly yes --appendfsync everysec` and healthcheck on `ticketreserver-internal-net`.
- [x] 1.2 Update root `docker-compose.yml` to include `./docker/redis/docker-compose.yml` and update `TR.API.Events/docker-compose.yml` with `ConnectionStrings__Redis` and `depends_on: redis: condition: service_healthy`, verifying via `docker compose config`.

## 2. Caching Contract & Redis Infrastructure Implementation

- [x] 2.1 Define `ICacheService` and `CacheKeys` in `TR.API.Events.Domain/Contracts/Caching/`, verifying compilation via `dotnet build`.
- [x] 2.2 Install `StackExchange.Redis` in `TR.API.Events.Infrastructure`, implement `RedisCacheService` in `TR.API.Events.Infrastructure/Caching/`, register `IConnectionMultiplexer` and `ICacheService` in `DependencyInjection.cs`, and update `appsettings.json`, verifying compilation via `dotnet build`.

## 3. Application L1 Cache-Aside & Automatic Invalidation

- [x] 3.1 Update `GetEventByIdQueryHandler` (`catalog:event:{id}`, TTL 1h) and `ListVenuesQueryHandler` (`catalog:venues`, TTL 1h) to implement L1 Cache-Aside via `ICacheService`, and verify unit tests in `TR.API.Events.UnitTests/Application/Events/CatalogQueryHandlerTests.cs` pass via `dotnet test`.
- [x] 3.2 Update `UpdateEventCommandHandler`, `ChangeEventStatusCommandHandler`, and `CancelEventCommandHandler` to invalidate `catalog:event:{id}` upon mutation, and verify unit tests in `TR.API.Events.UnitTests/Application/Events/` pass via `dotnet test`.

## 4. Readiness Probe & End-to-End Integration Verification

- [x] 4.1 Update `HealthModule` (`/health/ready`) in `TR.API.Events.WebApi/Endpoints/HealthModule.cs` to verify Redis connectivity alongside PostgreSQL, and verify `dotnet build` and `dotnet format --verify-no-changes`.
- [x] 4.2 Start containers via `docker compose up -d --build` and verify Redis `noeviction`/`appendonly` configuration, `/health/ready` status, L1 cache population (`catalog:event:{id}`, `catalog:venues`), and cache invalidation after `PUT`/`PATCH`/`DELETE`.
