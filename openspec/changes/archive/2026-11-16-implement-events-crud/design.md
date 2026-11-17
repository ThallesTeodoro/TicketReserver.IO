# Design: Implement Events CRUD and Lifecycle in TR.API.Events

## Context

See [proposal.md](file:///home/thallesteodoro/development/ticket-reservation-pdi/openspec/changes/implement-events-crud/proposal.md) for motivation and [catalog-events.md](file:///home/thallesteodoro/development/ticket-reservation-pdi/.agents/specification/db/catalog-events.md) for the physical database schema. `TR.API.Events` runs on .NET 10 and connects to `catalog_events_db` in PostgreSQL 16. The database enforces foreign keys (`events.venue_id -> venues.id`, `ticket_tiers.section_id -> seat_sections.id`), check constraints on date windows, and custom PostgreSQL enum types (`event_status_enum`, `ticket_tier_type_enum`, `ticket_tier_status_enum`).

## Goals / Non-Goals

**Goals:**
- Implement rich domain entities (`Event`, `Venue`, `SeatSection`, `TicketTier`) and value objects (`DateTimeRange`, `Money`) with zero external ORM annotations, private setters, and `IReadOnlyCollection<T>` navigations.
- Implement hybrid CQRS persistence: EF Core with `IEntityTypeConfiguration<T>` and `IUnitOfWork` for transactional writes, and strictly parameterized Dapper queries for fast reads.
- Expose English REST endpoints (`/events`, `/venues`) via Carter modules wrapped in `JsonResponse<TData, TError>`.
- Provide idempotent seeding for default `venues` and `seat_sections`.
- Ensure 100% unit test coverage of handlers and domain invariants using xUnit, Moq, and FluentAssertions.

**Non-Goals:**
- Full CRUD for `Venues` and `Seats` (only `GET /venues` and initial seed are in scope to unblock Event management).
- Redis caching layer or Debezium connector deployment (handled in subsequent dedicated changes).

## Decisions

### Decision 1: Rich Domain Model & State Machine
- **Decision:** Model `Event` with a private constructor and static factory `Event.Create(...)` that validates all invariants (`SalesWindow`, `Schedule`, `EventStartAt >= SalesStartAt`, `MaxTicketsPerUser > 0`, `AgeRestriction >= 0`) and initializes `Status = EventStatus.Draft` and `Version = 1`. State transitions are encapsulated in domain methods (`Publish()`, `OpenSales()`, `PauseSales()`, `MarkAsSoldOut()`, `Cancel()`), each validating allowed source states and incrementing `Version`.
- **Rationale:** Complies with `dotnet-carter-cqrs-guard` and `dotnet-efcore-pg-repository` hard blockers prohibiting anemic domain models and public setters.

### Decision 2: Logical Cancellation on `DELETE /events/{id}`
- **Decision:** Implement `DELETE /events/{id}` by invoking `CancelEventCommand`, which calls `event.Cancel()` to transition the status to `CANCELLED` and persists via EF Core instead of issuing a physical SQL `DELETE`.
- **Rationale:** In a distributed ticketing platform with CDC (Debezium -> Kafka -> Search/Reservation), physical deletion breaks downstream audit trails and active reservations. Logical cancellation preserves integrity and propagates a state transition event via CDC.

### Decision 3: PostgreSQL Native Enum Mapping in Npgsql 10
- **Decision:** Map `EventStatus`, `TicketTierType`, and `TicketTierStatus` using Npgsql's `NpgsqlDataSourceBuilder.MapEnum<TEnum>(...)` with `[PgName("...")]` attributes matching the exact uppercase PostgreSQL enum labels (`DRAFT`, `PUBLISHED`, `SALES_OPEN`, etc.), and cast enum columns to `::text` in Dapper read queries.
- **Rationale:** Avoids Npgsql type mismatch errors (`column "status" is of type event_status_enum but expression is of type text/integer`) during EF Core inserts/updates while keeping Dapper materialization simple and fast.

### Decision 4: Dual Idempotent Venue Seeding (SQL + Startup Initializer)
- **Decision:** Add `INSERT ... ON CONFLICT DO NOTHING` seed statements for default venues and seat sections to `docker/postgres/init-events-db.sql` AND execute an idempotent startup seed check in `TR.API.Events` using `IDbConnectionFactory`.
- **Rationale:** Docker entrypoint scripts only run when the volume `postgres_data` is first created. Running an idempotent seed check on API startup guarantees default venues exist even on already-initialized volumes without requiring destructive volume resets.

### Decision 5: Vertical Slice Organization in `TR.API.Events.Application`
- **Decision:** Organize each use case under `Application/Events/{UseCase}/` and `Application/Venues/ListVenues/`, with dedicated static mapping extension classes (`EventMappingExtensions.cs`) and FluentValidation validators for input syntax validation.
- **Rationale:** Follows `dotnet-cqrs-slice-generator` standards, keeping each slice self-contained and free of MediatR or AutoMapper.

## Risks / Trade-offs

- **[Risk] Unique Slug Collisions on Event Creation/Update:** Concurrent or duplicate slugs would trigger a database `uq_events_slug` constraint exception.
  - *Mitigation:* Check slug uniqueness via `IEventRepository.ExistsBySlugAsync` in the command handler before saving, throwing a domain/validation exception that translates to a clean 400/422 `JsonResponse`.
- **[Risk] FluentValidation vs Carter Handler Execution:** Validators registered in DI are not automatically invoked by Carter unless called in the handler or endpoint.
  - *Mitigation:* Inject `IValidator<TCommand>` into command handlers (or validate explicitly before domain execution) and throw `TR.API.Events.Domain.Exceptions.ValidationException` on failure so `ExceptionHandlerMiddleware` returns HTTP 400 with structured error details.
