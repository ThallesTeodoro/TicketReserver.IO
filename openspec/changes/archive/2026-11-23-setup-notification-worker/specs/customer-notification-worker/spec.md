# Spec Delta

## Purpose

Consumes terminal payment and ticketing events from Apache Kafka, guarantees idempotent processing via distributed inbox deduplication, and dispatches resilient Email and Push notifications to inform customers of ticket issuance, automatic payment refund, or payment decline.

## ADDED Requirements

### Requirement: Multi-Topic Terminal Event Consumption and Distributed Inbox Deduplication
The system SHALL consume messages from `ticketing.v1.issued`, `payment.v1.refunded`, and `payment.v1.declined` under consumer group `cg-notification-worker` and record processed message identifiers in a distributed inbox store (`notif:inbox:{consumerGroup}:{messageId}`) to prevent duplicate customer notifications.

#### Scenario: First delivery of terminal saga event
- **WHEN** a `ticketing.v1.issued`, `payment.v1.refunded`, or `payment.v1.declined` event is consumed
- **THEN** the worker verifies that `notif:inbox:cg-notification-worker:{messageId}` does not exist in the inbox store and proceeds with notification dispatch.

#### Scenario: Duplicate delivery of terminal saga event
- **WHEN** a message with an already recorded `notif:inbox:cg-notification-worker:{messageId}` is received
- **THEN** the worker skips notification dispatch and commits the Kafka offset immediately without sending duplicate Email or Push messages.

### Requirement: Transactional Notification Dispatch by Saga Outcome
The system SHALL dispatch multi-channel notifications (`Email` and `Push`) tailored to the specific saga outcome (`TicketIssuedConfirmation`, `PaymentRefundedNotice`, or `PaymentDeclinedNotice`).

#### Scenario: Ticket issuance confirmation notification
- **WHEN** a `ticketing.v1.issued` event is processed for a reservation
- **THEN** the system dispatches a ticket confirmation notification containing the reservation identifier and issued ticket references across configured channels, marks the inbox entry as processed, and commits the Kafka offset.

#### Scenario: Automatic payment refund notification
- **WHEN** a `payment.v1.refunded` event is processed for a reservation
- **THEN** the system dispatches an automatic refund notification informing the customer of the seat unavailability and full refund reference, marks the inbox entry as processed, and commits the Kafka offset.

#### Scenario: Payment decline notification
- **WHEN** a `payment.v1.declined` event is processed for a reservation
- **THEN** the system dispatches a payment declined notification informing the customer of the payment rejection reason, marks the inbox entry as processed, and commits the Kafka offset.

### Requirement: Resilient Provider Dispatch with Retry and Circuit Breaker
The system SHALL wrap external Email and Push notification gateway calls in a resilience policy using retry with exponential backoff and circuit breaking to tolerate transient provider outages.

#### Scenario: Transient notification provider failure recovers on retry
- **WHEN** the external notification provider returns a transient timeout or network error on the initial attempt
- **THEN** the dispatcher retries with exponential backoff until delivery succeeds and records the provider delivery reference.

### Requirement: Poison Pill Routing to Dead Letter Queue
The system SHALL route malformed or undeserializable notification payloads to the corresponding `.DLQ` topic with diagnostic headers and commit the consumer offset.

#### Scenario: Malformed payload routed to DLQ
- **WHEN** a message consumed from `ticketing.v1.issued`, `payment.v1.refunded`, or `payment.v1.declined` cannot be deserialized or fails mandatory schema validation
- **THEN** the worker publishes the raw payload to `<original-topic>.DLQ` with headers `x-original-topic`, `x-exception-message`, and `x-failed-at`, and commits the offset.
