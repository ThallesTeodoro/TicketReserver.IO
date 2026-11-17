# Spec Delta: catalog-events-api

## ADDED Requirements

### Requirement: Event Registration and Validation
The system SHALL allow registering a new event via `POST /events` in `DRAFT` status when associated with an existing venue, unique slug, and valid sales/event date ranges.

#### Scenario: Successful event creation
- **WHEN** a client sends a valid `POST /events` request with an existing `venueId`, unique `slug`, `salesEndAt > salesStartAt`, `eventEndAt > eventStartAt`, and `eventStartAt >= salesStartAt`
- **THEN** the API persists the event with status `DRAFT` and version `1`, returning HTTP 201 with the created event wrapped in `JsonResponse`.

#### Scenario: Reject creation with invalid date windows
- **WHEN** a client sends a `POST /events` request where `salesEndAt <= salesStartAt` or `eventStartAt < salesStartAt`
- **THEN** the API rejects the operation with HTTP 400 or HTTP 422 containing domain/validation errors in `JsonResponse`.

#### Scenario: Reject creation when venue does not exist
- **WHEN** a client sends a `POST /events` request referencing a non-existent `venueId`
- **THEN** the API returns HTTP 404 wrapped in `JsonResponse`.

### Requirement: Event Details and Paginated Queries
The system SHALL provide parameterized read endpoints `GET /events/{id}` and `GET /events` for retrieving event details and filtered paginated listings.

#### Scenario: Retrieve existing event by ID
- **WHEN** an HTTP GET request is sent to `/events/{id}` for an existing event
- **THEN** the API returns HTTP 200 with event details, venue summary, and associated ticket tiers wrapped in `JsonResponse`.

#### Scenario: Return 404 for unknown event ID
- **WHEN** an HTTP GET request is sent to `/events/{id}` for a non-existent event
- **THEN** the API returns HTTP 404 wrapped in `JsonResponse`.

#### Scenario: Paginated event listing with filters
- **WHEN** an HTTP GET request is sent to `/events?page=1&pageSize=10&category=CONCERT&status=SALES_OPEN`
- **THEN** the API returns HTTP 200 with matching events and pagination metadata wrapped in `JsonResponse`.

### Requirement: Event Update and Optimistic Versioning
The system SHALL allow updating an existing non-cancelled event via `PUT /events/{id}`, enforcing temporal invariants and incrementing the event `version`.

#### Scenario: Successful event update
- **WHEN** a client sends a valid `PUT /events/{id}` request for an existing active event
- **THEN** the API updates the event attributes, increments `version` by 1, updates `updatedAt`, and returns HTTP 200 wrapped in `JsonResponse`.

### Requirement: Event Lifecycle Transitions and Logical Cancellation
The system SHALL enforce valid state machine transitions (`DRAFT` -> `PUBLISHED` -> `SALES_OPEN` <-> `SALES_PAUSED` -> `SOLD_OUT`) via `PATCH /events/{id}/status` and logical cancellation (`CANCELLED`) via `DELETE /events/{id}`.

#### Scenario: Valid status transition
- **WHEN** a client sends `PATCH /events/{id}/status` transitioning a `DRAFT` event to `PUBLISHED`, or a `PUBLISHED` event to `SALES_OPEN`
- **THEN** the API updates the status, increments `version`, and returns HTTP 200 wrapped in `JsonResponse`.

#### Scenario: Invalid status transition rejected
- **WHEN** a client attempts an invalid transition such as moving a `DRAFT` event directly to `SOLD_OUT` or modifying a `CANCELLED` event
- **THEN** the API rejects the request with HTTP 422 wrapped in `JsonResponse`.

#### Scenario: Logical cancellation on DELETE
- **WHEN** an HTTP DELETE request is sent to `/events/{id}`
- **THEN** the API transitions the event status to `CANCELLED` without physically deleting the record and returns HTTP 200 wrapped in `JsonResponse`.

### Requirement: Venue Reference Listing
The system SHALL expose `GET /venues` returning registered venues and their seat sections to support event creation and catalog browsing.

#### Scenario: List available venues
- **WHEN** an HTTP GET request is sent to `/venues`
- **THEN** the API returns HTTP 200 with the list of venues and their associated seat sections wrapped in `JsonResponse`.
