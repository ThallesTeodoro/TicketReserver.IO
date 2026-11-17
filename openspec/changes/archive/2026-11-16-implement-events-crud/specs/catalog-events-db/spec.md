# Spec Delta: catalog-events-db

## ADDED Requirements

### Requirement: Deterministic Initial Venue and Section Seeding
The system SHALL seed default venues and seat sections idempotently so that event creation and catalog testing have valid relational targets out of the box.

#### Scenario: Default venues and sections are available after initialization
- **WHEN** the database initializes or the Event Catalog API starts up
- **THEN** at least two default venues and their seat sections exist in `venues` and `seat_sections` without duplicating records on subsequent restarts.
