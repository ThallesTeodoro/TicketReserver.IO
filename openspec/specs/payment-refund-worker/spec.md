# payment-refund-worker Specification

## Purpose

Consumes ticket issuance failure events, ensures idempotency against duplicate refunds, executes resilient payment gateway refunds, updates payment and refund ledgers transactionally, and dispatches refund completion events.

## Requirements

### Requirement: Ticketing Failure Consumption and Inbox Deduplication
The system SHALL consume messages from `ticketing.v1.failed` under consumer group `cg-payment-refund` and verify processed message identifiers in `inbox_messages` to guarantee idempotent refund execution.

#### Scenario: First delivery of ticketing failure event
- **WHEN** a `ticketing.v1.failed` event is consumed
- **THEN** the worker verifies that `(message_id, 'cg-payment-refund')` does not exist in `inbox_messages` and initiates the automatic refund flow.

#### Scenario: Duplicate delivery of ticketing failure event
- **WHEN** a message with an already recorded `(message_id, 'cg-payment-refund')` is received
- **THEN** the worker acknowledges and commits the offset immediately without invoking the payment gateway or modifying database records.

### Requirement: Captured Payment Verification and Single Refund Enforcement
The system SHALL verify that the payment associated with the reservation is in `CAPTURED` status before requesting a gateway refund and prevent duplicate refunds for the same payment.

#### Scenario: Refund eligible for captured payment
- **WHEN** a `CAPTURED` payment exists for the failed reservation
- **THEN** the system proceeds with the full amount refund request to the external payment gateway.

#### Scenario: Skip refund when payment is already refunded or not captured
- **WHEN** the payment is already `REFUNDED` or no `CAPTURED` payment exists for the reservation
- **THEN** the system skips the gateway refund call, records the message in `inbox_messages`, and commits the offset.

### Requirement: Resilient Payment Gateway Refund Execution
The system SHALL invoke the external payment gateway to refund the captured transaction, handling transient network or provider failures with retry and exponential backoff.

#### Scenario: Successful gateway refund
- **WHEN** the external payment gateway confirms the refund
- **THEN** the system receives the `gateway_refund_id`, transitions the refund to `COMPLETED` and the payment to `REFUNDED`, and emits a `payment.v1.refunded` event.

### Requirement: Atomic Financial Ledger and Outbox Persistence
The system SHALL atomically update the payment status, insert the refund record, append an immutable refund transaction ledger entry, and record outbox and inbox messages in a single PostgreSQL transaction.

#### Scenario: Atomic commit of refund state and outbox
- **WHEN** the gateway refund completes
- **THEN** updates to `payments` (`status = 'REFUNDED'`) and insertions into `refunds`, `payment_transactions` (`transaction_type = 'REFUND'`), `outbox_messages`, and `inbox_messages` commit together in a single transaction before manual Kafka offset acknowledgment.
