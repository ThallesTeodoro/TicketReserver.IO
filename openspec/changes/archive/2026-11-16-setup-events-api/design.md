# Design: Setup TR.API.Events (.NET 10 & Modular Compose)

## Context

See [proposal.md](file:///home/thallesteodoro/development/ticket-reservation-pdi/openspec/changes/setup-events-api/proposal.md) for motivation and requirements. The repository has a provisioned PostgreSQL 16 database (`ticketreserver-postgres`) running in `ticketreserver-internal-net`. We need to establish the foundational .NET 10 microservice for the Event Catalog API (`TR.API.Events`), structured according to Clean Architecture and CQRS Lite guidelines, and orchestrated via modular Docker Compose.

## Goals / Non-Goals

**Goals:**
- Establish the complete solution structure for `TR.API.Events` in .NET 10 across 5 isolated layers (`Domain`, `Application`, `Infrastructure`, `WebApi`, `UnitTests`).
- Implement native CQRS Lite abstractions, static extension mapping, FluentValidation, Carter Minimal APIs, `JsonResponse` envelope, and global exception handling.
- Build a production-ready, multi-stage .NET 10 `Dockerfile` for the service.
- Implement modular Docker Compose orchestration utilizing the native `include` directive in the root `docker-compose.yml`.
- Align project documentation ([PROJECT.md](file:///home/thallesteodoro/development/ticket-reservation-pdi/PROJECT.md)) with the namespace and folder conventions.

**Non-Goals:**
- Implement the full catalog business features (CRUD of Venues, Sections, Ticket Tiers, and Events) — these will follow in dedicated vertical slice changes.
- Implement Debezium CDC connectors or Kafka producers in this change (addressed in subsequent changes).
- Integrate Redis distributed cache in this change.

## Decisions

### Decision 1: Namespace and Component Folder Layout
- **Decision:** Place the component in `TR.API.Events/` at repo root, with projects named `TR.API.Events.[Domain|Application|Infrastructure|WebApi|UnitTests]`.
- **Rationale:** Aligns with the project specification naming standard (`TR.[API|HW|KFK].[Description]`) and ensures each service or worker owns its codebase, solution, Dockerfile, and compose snippet.
- **Alternatives considered:** Monolithic source folder (`src/Events.Api`, `src/Events.Domain`) — rejected because it mixes components in a multi-service ecosystem.

### Decision 2: CQRS Lite Native without MediatR or AutoMapper
- **Decision:** Define lightweight generic interfaces (`ICommand`, `ICommandHandler`, `IQuery`, `IQueryHandler`) in `Application/Common/Messaging/` registered via Assembly Scan. Use static extension methods for DTO mappings.
- **Rationale:** Strict compliance with [PROJECT.md](file:///home/thallesteodoro/development/ticket-reservation-pdi/PROJECT.md) hard blocker. Eliminates runtime reflection overhead and third-party lock-in.
- **Alternatives considered:** MediatR / AutoMapper — strictly prohibited by project governance rules.

### Decision 3: Minimal APIs via Carter with Uniform JsonResponse Envelope
- **Decision:** Implement API endpoints inheriting from `CarterModule`. All endpoints return `JsonResponse<TData, TError>` with `statusCode`, `message`, `data`, and `errors`.
- **Rationale:** Enforces clean separation of endpoint routing from business handlers and guarantees structured contracts for upstream consumers/gateways.

### Decision 4: Docker Compose Modularization via Native `include`
- **Decision:** Maintain `TR.API.Events/docker-compose.yml` defining the `events-api` service, and reference it from the root `docker-compose.yml` using `include: [ ./TR.API.Events/docker-compose.yml ]`.
- **Rationale:** Docker Compose v2.20+ supports `include:`. This prevents root `docker-compose.yml` sprawl across multiple services while allowing a single `docker compose up` command to orchestrate the entire platform.
- **Alternatives considered:** 
  - Monolithic single `docker-compose.yml`: becomes unmaintainable as 6+ services and workers are added.
  - Shell scripts with multiple `-f` flags: error-prone and developer-unfriendly.

### Decision 5: Multi-stage Dockerfile (.NET 10)
- **Decision:** Use .NET 10 SDK image for restore/build/publish and ASP.NET 10 runtime image for the final lightweight container. Set `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`.
- **Rationale:** Minimizes container footprint, leverages security best practices (non-root execution), and avoids libicu issues in containerized environments.

## Risks / Trade-offs

- **[Risk] Docker Compose `include` path resolution:** Relative paths in included compose files could resolve incorrectly if misconfigured.
  - *Mitigation:* Explicitly set `context: .` relative to `TR.API.Events` in `TR.API.Events/docker-compose.yml`. Docker Compose resolves build context relative to the included file location.
- **[Risk] Host vs Container Port Collision:** Port 5001 might be in use by local services.
  - *Mitigation:* Parameterize the host port with default `${EVENTS_API_PORT:-5001}:8080`.
- **[Risk] Compilation / Format Verification in .NET 10:**
  - *Mitigation:* Ensure `dotnet build` and `dotnet format --verify-no-changes` run cleanly during implementation gates.
