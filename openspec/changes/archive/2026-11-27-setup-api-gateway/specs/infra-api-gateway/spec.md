# Spec Delta

## Purpose

Provides a centralized, production-grade reverse proxy and API Gateway using NGINX Alpine to route HTTP traffic to catalog, reservation, and search microservices with unified CORS, W3C trace context propagation, and health monitoring.

## ADDED Requirements

### Requirement: Unified Reverse Proxy Routing for Core Microservices
The system SHALL provide an NGINX Alpine reverse proxy listening on port 80 (HTTP) that routes incoming client traffic to internal upstream services: `/events` and `/venues` to the Events API, `/reserva` to the Reservation API, and `/search` (rewriting to `/api/v1/search/events`) and `/api/v1/search` to the Search API.

#### Scenario: Routing Event Catalog Requests
- **WHEN** a client sends a GET request to `http://localhost/events` or `http://localhost/venues`
- **THEN** the gateway proxies the request to `http://events-api:8080/events` or `http://events-api:8080/venues` and returns the upstream response

#### Scenario: Routing Reservation Requests
- **WHEN** a client sends a POST request to `http://localhost/reserva/hold` or `http://localhost/reserva/checkout`
- **THEN** the gateway proxies the request to `http://reservation-api:8080/reserva/hold` or `http://reservation-api:8080/reserva/checkout` and returns the upstream response

#### Scenario: Routing Search Requests with URL Rewriting
- **WHEN** a client sends a GET request to `http://localhost/search?q=rock`
- **THEN** the gateway rewrites the path to `/api/v1/search/events?q=rock`, forwards it to `http://search-api:8080`, and returns the search results

### Requirement: W3C Trace Context and Proxy Header Propagation
The system SHALL forward incoming W3C TraceContext headers (`traceparent` and `tracestate`) and standard HTTP proxy headers (`Host`, `X-Real-IP`, `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Request-ID`) to upstream services, injecting an X-Request-ID when not supplied by the client.

#### Scenario: Preservation of Distributed Trace Headers
- **WHEN** an incoming HTTP request contains a W3C `traceparent` header
- **THEN** the gateway forwards the exact `traceparent` and `tracestate` headers to the upstream service without alteration

#### Scenario: Generation of Request Correlation ID
- **WHEN** an incoming HTTP request lacks an `X-Request-ID` header
- **THEN** the gateway generates a unique request identifier and attaches it to both upstream request headers and client response headers

### Requirement: Centralized Cross-Origin Resource Sharing Policy
The system SHALL intercept HTTP preflight `OPTIONS` requests and handle CORS headers centrally across all API routes, allowing standard methods (GET, POST, PUT, DELETE, OPTIONS) and headers (Content-Type, Authorization, traceparent).

#### Scenario: Handling CORS Preflight Request
- **WHEN** a browser client sends an HTTP `OPTIONS` request with `Origin` and `Access-Control-Request-Method` headers to any routed path
- **THEN** the gateway returns HTTP 204 No Content with appropriate `Access-Control-Allow-Origin`, `Access-Control-Allow-Methods`, and `Access-Control-Allow-Headers`

### Requirement: Gateway Health Monitoring Endpoint
The system SHALL expose an unauthenticated health check endpoint at `/healthz` on port 80 returning HTTP status 200 with JSON payload `{"status":"UP","gateway":"ticketreserver-ingress"}`.

#### Scenario: Health Check Query
- **WHEN** an automated orchestrator or monitor sends a GET request to `http://localhost/healthz`
- **THEN** the gateway immediately returns HTTP 200 OK with status UP without querying upstream services
