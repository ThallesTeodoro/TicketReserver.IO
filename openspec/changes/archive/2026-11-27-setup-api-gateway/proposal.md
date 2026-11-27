# Proposal

## Why

Currently, client applications and external consumers must interact with three distinct Web APIs across fragmented host ports (`events-api:5001`, `search-api:5003`, and `reservation-api:5003`). This exposes internal container topology, complicates client integration, lacks centralized CORS policies, and leaves the architecture without the single entrypoint specified in `arch-decisions.md` and `infra-spec.md`.

Providing an NGINX Alpine-based API Gateway / Ingress on port 80/443 establishes a unified entrypoint, cleanly abstracts internal service routing (including clean `/search` alias rewriting), propagates W3C distributed trace headers, and shields the `ticketreserver-internal-net` topology from external clients.

## What Changes

- **Add NGINX Ingress Container**: Create `docker/ingress/docker-compose.yml` defining the `ticketreserver-ingress` service based on `nginx:alpine`, bound to ports `80:80` and `443:443`, attached to both `ticketreserver-public-net` and `ticketreserver-internal-net`.
- **Authoritative Reverse Proxy Configuration**: Provide `docker/ingress/nginx.conf` with production-grade reverse proxy rules:
  - Routing `/events` and `/venues` to `http://events-api:8080`
  - Routing `/reserva/*` (`/hold`, `/checkout`, `/{id}`) to `http://reservation-api:8080`
  - Routing `/search` and `/search/*` with rewrite to `/api/v1/search/events` on `http://search-api:8080`, and pass-through for `/api/v1/search/*`
  - Gateway healthcheck endpoint at `/healthz` returning `200 OK`
- **W3C Trace & Proxy Header Propagation**: Forward incoming or generated `traceparent`, `tracestate`, `X-Request-ID`, `X-Forwarded-For`, `X-Forwarded-Proto`, and `Host` headers to maintain end-to-end observability across Grafana Tempo and Loki.
- **CORS & Buffer Optimization**: Centralized CORS preflight (`OPTIONS`) handling and proxy buffering/timeouts tuned for low latency under high concurrency.
- **Root Docker Compose Integration**: Add `./docker/ingress/docker-compose.yml` to the root `docker-compose.yml` `include:` directive.
- **Automated Ingress Verification**: Provide `./docker/ingress/verify-ingress.sh` for dry-run config validation and live routing verification against running containers.

## Capabilities

### New Capabilities

- `infra-api-gateway`: Centralized reverse proxy and Ingress gateway that routes external HTTP requests to catalog, reservation, and search microservices, enforcing W3C trace context preservation, health monitoring, and unified CORS.

### Modified Capabilities

None. Existing microservice contracts and endpoints remain intact and backwards-compatible.

## Impact

- **Docker Compose**: Root `docker-compose.yml` updated with `./docker/ingress/docker-compose.yml`.
- **Infrastructure**: New directory `docker/ingress/` containing `docker-compose.yml`, `nginx.conf`, and `verify-ingress.sh`.
- **Client Integration**: Clients can access all APIs via a single host (`http://localhost/events`, `http://localhost/venues`, `http://localhost/reserva/hold`, `http://localhost/search`).
- **Documentation**: Updates to `README.md` and `.agents/specification/infra/infra-spec.md`.
