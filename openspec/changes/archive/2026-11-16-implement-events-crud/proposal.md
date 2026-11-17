# Proposal: Implement Events CRUD and Lifecycle in TR.API.Events

## Why

With the `TR.API.Events` foundation and `catalog_events_db` database provisioned, the platform needs the core Event Catalog management endpoints (`POST`, `GET`, `PUT`, `PATCH`, `DELETE /events` and `GET /venues`) so organizers and downstream services can register, update, query, and manage the sales lifecycle of events.

## What Changes

- **Database Seeding (`catalog_events_db`)**:
  - Add deterministic initial seed data for `venues` and `seat_sections` (in `docker/postgres/init-events-db.sql` and ensured on application startup) to satisfy foreign key constraints (`fk_events_venue`, `fk_ticket_tiers_section`).
- **Rich Domain Model (`TR.API.Events.Domain`)**:
  - Implement Value Objects: `DateTimeRange` and `Money`.
  - Implement Entities: `Venue`, `SeatSection`, `Event`, and `TicketTier` with private setters, encapsulated `IReadOnlyCollection<T>` navigations, factory methods validating domain invariants, and state transition methods (`Publish()`, `OpenSales()`, `PauseSales()`, `Cancel()`, `MarkAsSoldOut()`).
  - Define repository and unit of work contracts (`IEventRepository`, `IUnitOfWork`).
- **Hybrid Persistence (`TR.API.Events.Infrastructure`)**:
  - Implement EF Core `IEntityTypeConfiguration<T>` mappings for `Venue`, `SeatSection`, `Event`, and `TicketTier` (including PostgreSQL enum mapping).
  - Implement `EventRepository` and `UnitOfWork` for transactional writes, plus startup database seeder for default venues.
- **Vertical Slices (`TR.API.Events.Application`)**:
  - `Events/CreateEvent`: Command, FluentValidation validator, handler, and static mapping extensions.
  - `Events/UpdateEvent`: Command, FluentValidation validator, handler, and static mapping extensions.
  - `Events/ChangeEventStatus`: Command and handler for lifecycle transitions (`PUBLISHED`, `SALES_OPEN`, `SALES_PAUSED`, `SOLD_OUT`).
  - `Events/CancelEvent`: Command and handler executing logical cancellation (`CANCELLED`).
  - `Events/GetEventById`: Parameterized Dapper query returning event details, venue metadata, and ticket tiers.
  - `Events/ListEvents`: Parameterized Dapper paginated query with optional filters (`category`, `status`, `city`).
  - `Venues/ListVenues`: Parameterized Dapper query returning available venues and their sections.
- **REST Endpoints (`TR.API.Events.WebApi`) & Unit Tests (`TR.API.Events.UnitTests`)**:
  - Expose Carter modules `EventsModule` (`/events`) and `VenuesModule` (`/venues`) wrapped in `JsonResponse<TData, TError>`.
  - Add comprehensive AAA unit tests for domain invariants and all application handlers.

## Capabilities

### New Capabilities
<!-- None -->

### Modified Capabilities
- `catalog-events-api`: Add requirements for Event CRUD operations, lifecycle status transitions, logical cancellation on delete, paginated event queries, and venue listing.
- `catalog-events-db`: Add requirement for deterministic seed data of default venues and seat sections.

## Impact

- **Domain & Application (`TR.API.Events`)**: Adds domain entities, value objects, repository contracts, and 7 vertical slice use cases.
- **Infrastructure (`TR.API.Events`)**: Maps domain entities to PostgreSQL tables and enums, implements `IEventRepository` and `IUnitOfWork`, and seeds initial venues.
- **API Surface**: Adds `/events` (`POST`, `GET`, `GET /{id}`, `PUT /{id}`, `PATCH /{id}/status`, `DELETE /{id}`) and `/venues` (`GET`).
- **Database (`docker/postgres/init-events-db.sql`)**: Appends idempotent `INSERT ... ON CONFLICT DO NOTHING` seed statements for default venues and sections.
