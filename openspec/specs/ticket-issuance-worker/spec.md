# ticket-issuance-worker Specification

## Purpose

Consumes payment approval events, enforces atomic seat allocations against race conditions, issues digitally signed tickets with cryptographic QR codes, and dispatches issuance or failure compensation events.

## Requirements

### Requirement: Approved Payment Event Consumption and Inbox Deduplication
The system SHALL consume messages from `payment.v1.approved` under consumer group `cg-ticketing-worker` and verify processed message identifiers in `inbox_messages` to ensure idempotent execution.

#### Scenario: First delivery of approved payment event
- **WHEN** a `payment.v1.approved` event is consumed
- **THEN** the worker verifies that `(message_id, 'cg-ticketing-worker')` does not exist in `inbox_messages` and initiates ticket issuance.

#### Scenario: Duplicate delivery of approved payment event
- **WHEN** a message with an already recorded `(message_id, 'cg-ticketing-worker')` is consumed
- **THEN** the worker acknowledges and commits the offset immediately without creating duplicate seat allocations or tickets.

### Requirement: Atomic Seat Allocation and Concurrency Barrier
The system SHALL persist itemized seat records into `seat_allocations` enforcing physical unique constraint `UNIQUE(event_id, seat_id)` to prevent double booking.

#### Scenario: Successful seat allocation
- **WHEN** an approved payment message arrives for unallocated seats
- **THEN** the system successfully inserts rows into `seat_allocations` and proceeds to generate digital tickets.

#### Scenario: Concurrent seat collision detected
- **WHEN** a concurrent worker or race condition attempts to allocate an already allocated `(event_id, seat_id)`
- **THEN** the database unique constraint violation is trapped, the transaction rolls back, and the worker records a failure compensation event.

### Requirement: Cryptographic QR Code and Digital Ticket Issuance
The system SHALL generate an immutable `tickets` record containing a cryptographically signed HMAC-SHA256 QR code hash for gate validation.

#### Scenario: Active ticket creation with cryptographic signature
- **WHEN** seat allocation succeeds
- **THEN** the system generates an HMAC-SHA256 signature containing ticket ID, event ID, customer ID, and issuance timestamp, setting ticket status to `ACTIVE`.

### Requirement: Transactional Outbox and Event Dispatching
The system SHALL atomically record issuance or failure events into `outbox_messages` in PostgreSQL and commit Kafka offsets manually.

#### Scenario: Successful issuance event publishing
- **WHEN** seat allocation and ticket generation commit successfully
- **THEN** an outbox record with event type `ticketing.v1.issued` is persisted in the same transaction, and the Kafka offset is committed.

#### Scenario: Compensation event publishing on allocation failure
- **WHEN** seat collision prevents ticket issuance
- **THEN** an outbox record with event type `ticketing.v1.failed` is persisted along with the inbox record, and the Kafka offset is committed to trigger the auto-refund saga.
