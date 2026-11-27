# Design

## Context

See [proposal.md](proposal.md) for motivation. The TicketReserver.IO platform orchestrates three ASP.NET Core 10 Web APIs (`TR.API.Events`, `TR.API.Reservation`, `TR.API.Search`), each running in self-contained Docker containers listening on internal port 8080 connected to the shared bridge network `ticketreserver-internal-net`.

The infrastructure specification (`infra-spec.md` Section 1 and Section 7) specifies an Ingress / API Gateway sitting in `ticketreserver-public-net` and bridging to `ticketreserver-internal-net` to provide single-entrypoint client access on port 80.

## Goals / Non-Goals

**Goals:**
- Provide a lightweight, high-performance reverse proxy using `nginx:1.27-alpine` bound to ports `80` and `443`.
- Route public paths predictably:
  - `/events` and `/venues` -> `http://events-api:8080`
  - `/reserva/*` -> `http://reservation-api:8080`
  - `/search` (rewriting to `/api/v1/search/events`) and `/api/v1/search/*` -> `http://search-api:8080`
  - `/healthz` -> Gateway health check returning HTTP 200 JSON
- Transparently preserve and propagate W3C Trace Context (`traceparent`, `tracestate`) and proxy headers (`Host`, `X-Real-IP`, `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Request-ID`).
- Centralize CORS preflight and response headers.
- Provide a standalone verification script (`./docker/ingress/verify-ingress.sh`) supporting both `--dry-run` and live container checks.

**Non-Goals:**
- Gateway-level JWT validation or OAuth2 token exchange (remains decoupled in application layers).
- Dynamic Lua scripting or external plugin runtimes.
- Automated ACME / Let's Encrypt TLS certificate provisioning (HTTP port 80 is the default local development target; port 443 is provisioned with self-signed fallback).

## Decisions

### 1. Reverse Proxy Engine: NGINX Alpine
- **Choice**: `nginx:1.27-alpine` container image with declarative `nginx.conf`.
- **Rationale**: Minimal footprint (~10MB memory usage), instant startup, zero compilation dependencies, and strict alignment with `infra-spec.md`.
- **Alternatives Considered**:
  - *Traefik v3*: Offers web dashboard and dynamic Docker label discovery, but requires mounting the host Docker socket (`/var/run/docker.sock`) or separate configuration.
  - *YARP (.NET 10)*: Consistent with the C# stack, but adds a fourth .NET solution requiring compilation, multi-stage Docker builds, and increased memory footprint.

### 2. Network Topology & Port Mapping
- **Choice**: The ingress container attaches to two bridge networks:
  - `ticketreserver-public-net`: Exposes ports `${GATEWAY_HTTP_PORT:-80}:80` and `${GATEWAY_HTTPS_PORT:-443}:443` to the host.
  - `ticketreserver-internal-net`: Allows direct DNS resolution and HTTP communication with `events-api:8080`, `reservation-api:8080`, and `search-api:8080`.
- **Rationale**: Ensures complete network isolation while allowing external clients to reach internal APIs exclusively through the gateway.

### 3. URL Rewriting for Search Autocomplete & Search Queries
- **Choice**: Configure NGINX rewrite rules:
  - `rewrite ^/search$ /api/v1/search/events break;`
  - `rewrite ^/search/(.*)$ /api/v1/search/events/$1 break;`
  - `proxy_pass http://search_upstream;`
- **Rationale**: Provides external clients with clean, intuitive endpoints (`GET /search?q=rock`, `GET /search/autocomplete?q=rock`) without exposing internal API versioning paths, while maintaining full pass-through support for `/api/v1/search/`.

### 4. W3C Distributed Trace Context and Proxy Headers
- **Choice**: NGINX configuration explicitly sets:
  ```nginx
  proxy_set_header Host $host;
  proxy_set_header X-Real-IP $remote_addr;
  proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
  proxy_set_header X-Forwarded-Proto $scheme;
  proxy_set_header X-Request-ID $request_id;
  proxy_set_header traceparent $http_traceparent;
  proxy_set_header tracestate $http_tracestate;
  ```
- **Rationale**: Ensures complete trace correlation in Grafana Tempo and log correlation in Grafana Loki starting from the moment a request enters the gateway.

## Risks / Trade-offs

- **[Port 80 Conflict on Host Machine]** → Configured as `${GATEWAY_HTTP_PORT:-80}:80`, allowing developers with local web servers to override the host port via `.env` (e.g. `GATEWAY_HTTP_PORT=8080`).
- **[Upstream Startup Race Condition]** → Ingress specifies `depends_on` with `condition: service_started` for `events-api`, `reservation-api`, and `search-api` to ensure containers are resolvable via Docker DNS before routing traffic.
