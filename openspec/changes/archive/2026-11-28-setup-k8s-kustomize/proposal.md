# Proposal

## Why

The TicketReserver.IO platform consists of 11 independent .NET components (3 Web APIs and 8 Background Workers) currently running alongside stateful infrastructure solely via Docker Compose. While Docker Compose is effective for single-host integration, it lacks container orchestration capabilities essential for high-throughput resilience testing: horizontal pod autoscaling (HPA), event-driven consumer autoscaling (KEDA Kafka lag metrics), granular resource allocation (CPU/RAM limits), zero-downtime rolling updates, and declarative environment-specific configuration via ConfigMaps and Secrets.

Adopting Kubernetes manifests organized with **Kustomize** (`k8s/base/` and `k8s/overlays/local/`) allows the platform to run its application tier on a local Kubernetes cluster (Kind, Minikube, K3d, or Docker Desktop K8s) while continuing to interface seamlessly with existing stateful data infrastructure (Postgres, Kafka, Redis, ElasticSearch, OTel Collector) through Kubernetes `ExternalName` services resolving to `host.docker.internal`. This provides an authentic cloud-native operational experience for local development and PDI exploration without requiring immediate cloud provider expenses or disruption of existing workflows.

## What Changes

- **Kustomize Base Architecture (`k8s/base/`)**:
  - Declarative Deployment manifests for all 3 Web APIs (`tr-api-events`, `tr-api-reservation`, `tr-api-search`) with readiness/liveness HTTP probes, resource requests/limits, and ClusterIP Services.
  - Declarative Deployment manifests for all 8 Background Workers (`tr-hw-events-cdc-relay`, `tr-hw-search-replication`, `tr-hw-res-outbox-relay`, `tr-hw-res-timeout`, `tr-hw-res-compensation`, `tr-hw-payment`, `tr-hw-refund`, and `tr-hw-ticketing`).
  - Autoscaling manifests:
    - Standard Kubernetes `HorizontalPodAutoscaler` (v2) for Web APIs based on average CPU (70%) and Memory (80%) utilization.
    - KEDA `ScaledObject` templates for Kafka consumer workers scaling based on consumer group lag thresholds.
    - Explicit singleton constraint (replica count locked at 1, no autoscaler) for `tr-hw-res-outbox-relay` to prevent concurrent database polling collisions.
  - Unified Ingress resource routing HTTP paths (`/events`, `/venues`, `/reserva`, `/search`, `/healthz`) to the respective API ClusterIP services.
  - Hybrid `ExternalName` Services mapping internal Kubernetes DNS names (`postgres-service`, `kafka-service`, `redis-service`, `elasticsearch-service`, `otel-collector-service`) to `host.docker.internal`.
  - Base `ConfigMap` and `Secret` declarations specifying application runtime variables, connection strings, and OTLP exporter targets.
- **Local Overlay (`k8s/overlays/local/`)**:
  - `kustomization.yaml` combining base resources with local image tags (`latest`), local replica baselines, and local development configurations.
  - Local Secrets for database passwords, Kafka credentials, and Redis configuration.
- **Local Cluster Automation Scripts (`scripts/`)**:
  - Shell scripts (`scripts/k8s-local-up.sh` and `scripts/k8s-local-down.sh`) to automate building/tagging local container images, applying Kustomize overlays, verifying rollout status, and testing endpoint reachability.

## Capabilities

### New Capabilities

- `infra-k8s-kustomize`: Declarative Kubernetes manifests managed via Kustomize for deploying, networking, and autoscaling all 11 TicketReserver.IO application components and Ingress in a local Kubernetes cluster, coupled with ExternalName bridges to local data infrastructure.

### Modified Capabilities

None. Existing microservice source code, Dockerfiles, and API contracts remain unchanged.

## Impact

- **Infrastructure & Repository**:
  - New directory `k8s/` containing `base/` (deployments, services, ingress, hpa, keda, external-services, config) and `overlays/local/`.
  - New operational helper scripts in `scripts/` for local cluster provisioning and teardown.
- **Dependencies**:
  - Local Kubernetes cluster tool (Kind, Minikube, K3d, or Docker Desktop K8s) and `kubectl` with native Kustomize support (`kubectl apply -k`).
  - Optional KEDA CRD installation for testing event-driven worker autoscaling.
- **Compatibility**:
  - Fully backward-compatible: existing Docker Compose stack remains operational and continues to host all databases, messaging, and telemetry backends.
