# Spec Delta: payment-processing-worker

## Purpose

Consumes reservation checkout requests, ensures idempotency against duplicate billing, executes resilient payment gateway authorizations, records immutable financial transaction ledgers, and dispatches approval and decline events.

## ADDED Requirements

### Requirement: Checkout Event Consumption and Inbox Deduplication
The system SHALL consume messages from `reservation.v1.checkout_req` under consumer group `cg-payment-processing` and verify processed message identifiers in `inbox_messages` to guarantee idempotent execution.

#### Scenario: First delivery of checkout request
- **WHEN** a `reservation.checkout_req` message is consumed from `reservation.v1.checkout_req`
- **THEN** the worker verifies that `(message_id, 'cg-payment-processing')` does not exist in `inbox_messages` and initiates payment processing.

#### Scenario: Duplicate delivery of checkout request
- **WHEN** a message with an already recorded `(message_id, 'cg-payment-processing')` is received
- **THEN** the worker acknowledges and commits the offset immediately without re-invoking the payment gateway or creating duplicate database records.

### Requirement: Resilient Payment Gateway Authorization and Capture
The system SHALL invoke the external payment gateway to authorize and capture the checkout amount, handling transient network failures with retry and exponential backoff.

#### Scenario: Successful payment gateway authorization and capture
- **WHEN** the payment gateway returns an approved response
- **THEN** the system transitions the payment status to `CAPTURED`, stores the `gateway_transaction_id` and `authorization_code`, and emits a `payment.v1.approved` event.

#### Scenario: Declined payment from gateway
- **WHEN** the payment gateway returns a declined response (e.g., insufficient funds or anti-fraud decline)
- **THEN** the system transitions the payment status to `DECLINED`, stores the `failure_reason`, and emits a `payment.v1.declined` event.

### Requirement: Transactional Ledger and Event Publishing
The system SHALL atomically record payment status, immutable transaction ledger entries, and outbox messages within a single database transaction.

#### Scenario: Atomic commit of payment and outbox
- **WHEN** a payment authorization completes
- **THEN** records in `payments`, `payment_transactions`, `outbox_messages`, and `inbox_messages` commit together in a single PostgreSQL transaction.

#### Scenario: Offset commit on transaction completion
- **WHEN** database transaction and outbox insertion commit successfully
- **THEN** the worker commits the Kafka offset manually.

### Requirement: Single Payment Enforcement per Reservation
The system SHALL strictly enforce that a reservation cannot be paid more than once, rejecting subsequent checkout attempts for an already paid reservation.

#### Scenario: Reject second checkout for same reservation
- **WHEN** a checkout request arrives for a reservation that already has an existing `CAPTURED` payment
- **THEN** the system rejects duplicate payment creation and skips redundant gateway charges.
