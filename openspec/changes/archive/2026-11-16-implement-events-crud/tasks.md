# Tasks: Implement Events CRUD and Lifecycle in TR.API.Events

## 1. Rich Domain Model & Contracts

- [x] 1.1 Implement Value Objects (`DateTimeRange`, `Money`) and Domain Enums (`EventStatus`, `TicketTierType`, `TicketTierStatus`) in `TR.API.Events.Domain`, verifying clean compilation via `dotnet build`.
- [x] 1.2 Implement rich entities (`Venue`, `SeatSection`, `Event`, `TicketTier`) with private setters, `IReadOnlyCollection<T>`, `Event.Create` invariant validation, and lifecycle state machine methods (`Publish`, `OpenSales`, `PauseSales`, `MarkAsSoldOut`, `Cancel`), and verify domain unit tests in `TR.API.Events.UnitTests/Domain/EventTests.cs` pass via `dotnet test`.
- [x] 1.3 Define persistence contracts (`IEventRepository`, `IUnitOfWork`) in `TR.API.Events.Domain/Contracts/Persistence/`, verifying project compilation.

## 2. Persistence & Database Seeding

- [x] 2.1 Implement EF Core `IEntityTypeConfiguration<T>` mappings (`VenueConfiguration`, `SeatSectionConfiguration`, `EventConfiguration`, `TicketTierConfiguration`), Npgsql enum mappings, `EventRepository`, and `UnitOfWork` in `TR.API.Events.Infrastructure`, verifying `dotnet build`.
- [x] 2.2 Add deterministic default `venues` and `seat_sections` seed statements to `docker/postgres/init-events-db.sql` and implement an idempotent startup database seeder in `TR.API.Events.Infrastructure`, verifying compilation.

## 3. Application Vertical Slices - Commands

- [x] 3.1 Implement `CreateEvent` and `UpdateEvent` vertical slices (Commands, FluentValidation validators, static mapping extensions, and Handlers) in `TR.API.Events.Application/Events/` and verify unit tests in `TR.API.Events.UnitTests/Application/Events/` pass via `dotnet test`.
- [x] 3.2 Implement `ChangeEventStatus` and `CancelEvent` vertical slices in `TR.API.Events.Application/Events/` and verify unit tests in `TR.API.Events.UnitTests/Application/Events/` pass via `dotnet test`.

## 4. Application Vertical Slices - Queries (Dapper)

- [x] 4.1 Implement parameterized Dapper query slices `GetEventById`, `ListEvents`, and `ListVenues` in `TR.API.Events.Application/` and verify unit tests in `TR.API.Events.UnitTests/Application/` pass via `dotnet test`.

## 5. Carter HTTP Endpoints & Integration Verification

- [x] 5.1 Implement `EventsModule` (`/events`) and `VenuesModule` (`/venues`) Carter endpoints in `TR.API.Events.WebApi/Endpoints/` wrapped in `JsonResponse<TData, TError>`, verifying `dotnet build` and `dotnet format --verify-no-changes`.
- [x] 5.2 Rebuild and start containers via `docker compose up -d --build` and verify end-to-end HTTP flows (`GET /venues`, `POST /events`, `GET /events`, `GET /events/{id}`, `PUT /events/{id}`, `PATCH /events/{id}/status`, `DELETE /events/{id}`).
