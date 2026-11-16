# Spec Delta: catalog-events-api

## Purpose

Provides the HTTP REST API and foundational application runtime for the Event Catalog service (`TR.API.Events`), implementing standardized response envelopment, health checks, and modular container orchestration.

## ADDED Requirements

### Requirement: Health and Readiness Diagnostics
The system SHALL expose standardized HTTP health check endpoints indicating service liveness and database readiness.

#### Scenario: Service liveness check passes
- **WHEN** an HTTP GET request is sent to `/health`
- **THEN** the API returns HTTP 200 with status indicating Healthy.

#### Scenario: Database readiness check
- **WHEN** an HTTP GET request is sent to `/health/ready` while the PostgreSQL database is reachable
- **THEN** the API returns HTTP 200 confirming database connectivity.

### Requirement: Uniform JSON Response Envelopment
The system SHALL format all HTTP responses through a consistent `JsonResponse<TData, TError>` envelope containing `statusCode`, `message`, `data`, and `errors`.

#### Scenario: Successful response formatting
- **WHEN** a client invokes an API endpoint that succeeds
- **THEN** the response payload contains the structured JSON envelope with `statusCode`, localized HTTP reason `message`, non-null `data`, and null `errors`.

#### Scenario: Handled error formatting
- **WHEN** a client request results in a validation error or known exception
- **THEN** the global exception handler returns the structured JSON envelope with corresponding HTTP status code, error `message`, null `data`, and populated `errors` details.

### Requirement: Modular Compose Integration
The system SHALL support containerized runtime execution via a dedicated service definition included in the root Docker Compose setup.

#### Scenario: Container starts within internal network
- **WHEN** the container stack is launched via root `docker compose up`
- **THEN** the `events-api` container starts, joins `ticketreserver-internal-net`, and resolves the PostgreSQL container by hostname.
