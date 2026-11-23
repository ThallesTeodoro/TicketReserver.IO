# Spec Delta: reservation-compensation-worker

## Purpose

Consumes terminal payment and ticketing events from Apache Kafka, transitions reservations in `reservation_db` to `CONFIRMED` or `CANCELLED`, and atomically releases Redis seat hold locks upon cancellation.

## ADDED Requirements

### Requirement: Multi-Topic Saga Event Consumption and Inbox Deduplication
The system SHALL consume messages from `payment.v1.refunded`, `payment.v1.declined`, and `ticketing.v1.issued` under consumer group `cg-reservation-compensator` and record processed message identifiers in `inbox_messages` to ensure idempotent execution.

#### Scenario: First delivery of terminal saga event
- **WHEN** a `payment.v1.refunded`, `payment.v1.declined`, or `ticketing.v1.issued` event is consumed
- **THEN** the worker verifies that `(message_id, 'cg-reservation-compensator')` does not exist in `inbox_messages` and initiates reservation state reconciliation.

#### Scenario: Duplicate delivery of terminal saga event
- **WHEN** a message with an already recorded `(message_id, 'cg-reservation-compensator')` is received
- **THEN** the worker acknowledges and commits the offset immediately without executing duplicate database updates or Redis mutations.

### Requirement: Reservation Confirmation on Ticket Issuance
The system SHALL transition the reservation status to `CONFIRMED` when a `ticketing.v1.issued` event is processed.

#### Scenario: Confirm reservation after ticket issuance
- **WHEN** a `ticketing.v1.issued` message arrives for a reservation in `PROCESSING_PAYMENT` or `AWAITING_PAYMENT` status
- **THEN** the system updates the reservation status to `CONFIRMED`, sets `confirmed_at`, increments `version`, records the inbox message, and commits the Kafka offset.

### Requirement: Reservation Cancellation on Payment Refund or Decline
The system SHALL transition the reservation status to `CANCELLED` with an explicit `cancellation_reason` when a `payment.v1.refunded` or `payment.v1.declined` event is processed.

#### Scenario: Cancel reservation on automatic saga refund
- **WHEN** a `payment.v1.refunded` event is consumed for a reservation
- **THEN** the system updates the reservation status to `CANCELLED`, sets `cancelled_at` and `cancellation_reason` (e.g., `AUTO_REFUND_SEAT_COLLISION`), and records the inbox message in a single transaction.

#### Scenario: Cancel reservation on payment decline
- **WHEN** a `payment.v1.declined` event is consumed for a reservation
- **THEN** the system updates the reservation status to `CANCELLED`, sets `cancelled_at` and `cancellation_reason` (e.g., `PAYMENT_DECLINED`), and records the inbox message in a single transaction.

### Requirement: Immediate Distributed Seat Lock Release on Cancellation
The system SHALL atomically release Redis seat hold keys (`hold:event:{eventId}:seat:{seatId}`) owned by a cancelled reservation without deleting keys owned by other reservations.

#### Scenario: Release Redis seat hold matching cancelled reservation
- **WHEN** a reservation with numbered seats transitions to `CANCELLED`
- **THEN** the worker executes a Lua script for each seat key `hold:event:{eventId}:seat:{seatId}` that deletes the key if and only if its value matches the cancelled `reservationId`.
