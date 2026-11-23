# search-replication-worker Specification

## Purpose

Consumes change data capture (CDC) event streams from Apache Kafka, denormalizes relational event, venue, and ticket tier changes into structured search documents, and updates the ElasticSearch search index with real-time upserts and partial updates.

## Requirements

### Requirement: Multi-Topic CDC Stream Consumption
The system SHALL consume CDC messages from `catalog.events.events`, `catalog.events.venues`, and `catalog.events.ticket_tiers` under consumer group `cg-search-replication-worker` with manual offset acknowledgment.

#### Scenario: Consume CDC event mutation
- **WHEN** a change event arrives on `catalog.events.events`
- **THEN** the worker parses the change operation (`op`), extracts before/after state, and processes document synchronization before acknowledging the Kafka offset.

#### Scenario: Consume ticket tier pricing mutation
- **WHEN** a change event arrives on `catalog.events.ticket_tiers`
- **THEN** the worker extracts the modified ticket tier and schedules a partial pricing and tier summary update on the corresponding event document.

### Requirement: Event Document Denormalization and Upsert
The system SHALL aggregate event and venue details into an `EventSearchDocument` and execute an upsert operation into ElasticSearch index `events_search`.

#### Scenario: Create or update event search document
- **WHEN** an event record is created or updated in the catalog
- **THEN** the system indexes an `EventSearchDocument` containing title, slug, description, category, status, schedule dates, venue details with geo-point location, and tags into ElasticSearch.

### Requirement: Ticket Tier Partial Pricing Synchronization
The system SHALL synchronize ticket tier price ranges (`min_price`, `max_price`) and tier lists (`tiers_summary`) for an event upon ticket tier mutations.

#### Scenario: Update event price range when tier price changes
- **WHEN** a ticket tier for an event is inserted or updated with a new price
- **THEN** the system updates the event's `pricing.min_price`, `pricing.max_price`, and `tiers_summary` in ElasticSearch.

### Requirement: Event Cancellation and Deletion Handling
The system SHALL reflect event cancellations or hard deletions by updating the document status to `CANCELLED` or deleting the document from ElasticSearch.

#### Scenario: Mark event as cancelled
- **WHEN** a CDC mutation indicates an event status change to `CANCELLED`
- **THEN** the document status in ElasticSearch is updated to `CANCELLED`.

#### Scenario: Delete event document on database deletion
- **WHEN** a CDC delete operation (`op == 'd'`) is consumed for an event
- **THEN** the document is removed from the ElasticSearch index.

### Requirement: Dead Letter Queue Fallback for Poison Pills
The system SHALL forward malformed CDC payloads to the designated `.DLQ` topic with diagnostic headers and commit the consumer offset.

#### Scenario: Malformed CDC message routed to DLQ
- **WHEN** a message cannot be parsed or deserialized
- **THEN** the raw payload is published to `<topic>.DLQ` with diagnostic headers and the consumer offset is committed.
