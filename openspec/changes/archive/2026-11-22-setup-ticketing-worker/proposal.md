# Proposal

## Why

Following payment authorization in `TR.HW.Payment`, tickets must be securely issued, itemized seats allocated with physical database guarantees against race conditions, and cryptographic anti-fraud QR codes generated. If a seat collision occurs due to concurrent execution, the system must trigger an automatic compensation saga (`ticketing.v1.failed`) to refund the customer.

## What Changes

- Create PostgreSQL initialization script `docker/postgres/init-ticketing-db.sql` with tables `tickets`, `seat_allocations`, `ticket_checkins`, `outbox_messages`, `inbox_messages`, and a strict unique constraint on `(event_id, seat_id)`.
- Scaffold `TR.HW.Ticketing` solution with clean separation across 5 projects (`Domain`, `Application`, `Infrastructure`, `Worker`, `UnitTests`) following .NET 10 and CQRS Lite guidelines (zero MediatR and zero AutoMapper).
- Implement `Ticket` aggregate root, `SeatAllocation` entity, cryptographic HMAC-SHA256 signature service, and domain events (`TicketIssuedDomainEvent`, `TicketingFailedDomainEvent`).
- Implement CQRS Lite `IssueTicketsCommand` and `IssueTicketsCommandHandler` orchestrating idempotency checks, atomic seat allocation, ticket creation, and transactional outbox.
- Implement `TicketingRepository` using Dapper and Npgsql handling PostgreSQL error code `23505 (unique_violation)` to detect seat collision and atomically record `ticketing.v1.failed` for compensation.
- Implement `KafkaTicketingProducer` with W3C `traceparent` context propagation, topic routing for `ticketing.v1.issued` and `ticketing.v1.failed`, and DLQ fallback.
- Implement `TicketIssuanceWorker` (`BackgroundService`) consuming `payment.v1.approved` under consumer group `cg-ticketing-worker` with manual offset acknowledgment.
- Configure multi-stage Dockerfile and docker-compose service integration.

## Capabilities

### New Capabilities
- `ticket-issuance-worker`: Consumes approved payment events, allocates numbered seats atomically, issues HMAC-SHA256 signed digital tickets, and publishes `ticketing.v1.issued` or `ticketing.v1.failed`.

### Modified Capabilities
<!-- None -->

## Impact

- **New Projects**: `TR.HW.Ticketing` (`Domain`, `Application`, `Infrastructure`, `Worker`, `UnitTests`).
- **Database**: New PostgreSQL database `ticketing_db` initialized via docker mount.
- **Kafka**: New consumer for `payment.v1.approved` and producer for `ticketing.v1.issued`, `ticketing.v1.failed`, and DLQs.
- **Root Docker Compose**: Registration of `ticketreserver-ticketing-worker` and `init-ticketing-db.sql`.
