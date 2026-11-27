# Tasks

## 1. NGINX Gateway Configuration and Compose Definition

- [x] 1.1 Create `docker/ingress/nginx.conf` with upstream definitions (`events-api:8080`, `reservation-api:8080`, `search-api:8080`), route mappings (`/events`, `/venues`, `/reserva`, `/search` rewrite, `/api/v1/search`), W3C trace headers (`traceparent`, `tracestate`, `X-Request-ID`), CORS preflight, and `/healthz`, and verify configuration syntax with `nginx -t` via Docker
- [x] 1.2 Create `docker/ingress/docker-compose.yml` defining `ticketreserver-ingress` using `nginx:1.27-alpine`, port mappings (`80:80`, `443:443`), dual network attachments (`ticketreserver-public-net`, `ticketreserver-internal-net`), and healthchecks, and verify with `docker compose -f docker/ingress/docker-compose.yml config`

## 2. Platform Orchestration and Automation

- [x] 2.1 Update root `docker-compose.yml` to include `./docker/ingress/docker-compose.yml` and declare `ticketreserver-public-net`, and verify the unified orchestration with `docker compose config --quiet`
- [x] 2.2 Create `./docker/ingress/verify-ingress.sh` supporting `--dry-run` configuration validation and live curl routing checks for `/healthz`, `/events`, `/reserva`, and `/search`, making the script executable

## 3. Integration Verification and Documentation

- [x] 3.1 Execute `./docker/ingress/verify-ingress.sh --dry-run` and verify that all configuration files, route mappings, and headers pass validation
- [x] 3.2 Update `README.md` and `.agents/specification/infra/infra-spec.md` documenting the unified API Gateway entrypoint, route table, and verify all markdown links

## Workflow follow-up

- Run `openspec validate setup-api-gateway --strict` to verify spec compliance.
- Apply the change via `/openspec-apply-change setup-api-gateway`.
- Archive the change after implementation and validation are complete.
