# reservation-timeout-worker Specification

## Purpose

Consumes delayed reservation hold events from Apache Kafka, performs idempotent reconciliation against the reservation database, expires unpaid holds in PostgreSQL, and releases seat locks in Redis.

## Requirements

### Requirement: Delayed Hold Event Consumption and Inbox Idempotency
The system SHALL consume messages from `reservation.v1.hold_created.delayed` under consumer group `cg-reservation-timeout` and record processed message identifiers into the `inbox_messages` table to guarantee idempotent execution.

#### Scenario: First delivery of delayed hold event
- **WHEN** a `reservation.hold_created` message is consumed from `reservation.v1.hold_created.delayed`
- **THEN** the worker checks `inbox_messages` for `(message_id, 'cg-reservation-timeout')`, processes the reservation, records the message in `inbox_messages`, and commits the Kafka offset.

#### Scenario: Duplicate delivery of delayed hold event
- **WHEN** a message with an already recorded `(message_id, 'cg-reservation-timeout')` is received
- **THEN** the worker acknowledges and commits the offset without executing duplicate database updates or Redis key mutations.

### Requirement: Conditional Reservation Expiration
The system SHALL inspect the reservation status and expire unpaid holds while leaving progressing or completed checkouts intact.

#### Scenario: Expire reservation awaiting payment
- **WHEN** the consumed reservation has status `AWAITING_PAYMENT`
- **THEN** the system updates the reservation status to `EXPIRED`, sets `cancellation_reason` to `PAYMENT_TIMEOUT`, and records `updated_at`.

#### Scenario: Ignore reservation already processing payment or confirmed
- **WHEN** the consumed reservation has status `PROCESSING_PAYMENT` or `CONFIRMED`
- **THEN** the system preserves the existing reservation status and records no cancellation reason.

### Requirement: Atomic Distributed Seat Lock Release
The system SHALL atomically release Redis seat hold locks corresponding to expired reservation items without clearing keys owned by subsequent reservations.

#### Scenario: Release Redis seat lock matching reservation
- **WHEN** a reservation with itemized seat holds is transitioned to `EXPIRED`
- **THEN** the worker executes a Lua script for each seat key `hold:event:{eventId}:seat:{seatId}` that deletes the key if and only if its value matches the expired `reservationId`.

#### Scenario: Retain Redis seat lock owned by another reservation
- **WHEN** the Redis key `hold:event:{eventId}:seat:{seatId}` contains a different reservation ID
- **THEN** the Lua script preserves the key and returns zero deleted keys.

### Requirement: Distributed Tracing and Manual Offset Acknowledgment
The system SHALL propagate distributed tracing context from incoming Kafka headers and acknowledge Kafka offsets manually only after database state commits.

#### Scenario: Context propagation from Kafka message
- **WHEN** a Kafka message arrives with a W3C `traceparent` header
- **THEN** the worker extracts the traceparent and sets the current activity context so downstream database and cache operations share the same trace ID.

#### Scenario: Offset commit on transaction completion
- **WHEN** database update and Redis cleanup succeed
- **THEN** the worker manually commits the message offset to Kafka.
