# Design

## Context

The TicketReserver.IO platform comprises 11 .NET 10 microservices and workers recently reorganized under `components/`, each equipped with a self-contained multi-stage `Dockerfile`. Stateful dependencies (PostgreSQL databases, Redis, Apache Kafka, ElasticSearch, and OpenTelemetry Collector) run reliably via Docker Compose on host ports (`5432`, `6379`, `9094`, `9200`, `4317`).

To introduce horizontal scaling, event-driven scaling, and container orchestration for local development and PDI exploration (see `proposal.md` - Why), we adopt Kubernetes manifests managed via **Kustomize**. The application tier runs inside a local Kubernetes cluster (Kind, Minikube, K3d, or Docker Desktop), while persisting connections to the local Docker Compose data layer through Kubernetes `ExternalName` services.

## Goals / Non-Goals

**Goals:**
- Provide a clean, modular Kustomize directory hierarchy (`k8s/base/` and `k8s/overlays/local/`) validated with standard `kubectl kustomize`.
- Define Deployments and ClusterIP Services for all 3 Web APIs (`tr-api-events`, `tr-api-reservation`, `tr-api-search`) with health probes (`/healthz`) and resource limits.
- Define Deployments for all 8 Background Workers (`tr-hw-events-cdc-relay`, `tr-hw-search-replication`, `tr-hw-res-outbox-relay`, `tr-hw-res-timeout`, `tr-hw-res-compensation`, `tr-hw-payment`, `tr-hw-refund`, and `tr-hw-ticketing`).
- Establish automated scaling policies:
  - HPA v2 for Web APIs based on CPU/RAM thresholds.
  - KEDA ScaledObjects for Kafka-consuming workers based on consumer group lag.
  - Enforce strict single-replica execution (replica = 1, no HPA/KEDA) for `tr-hw-res-outbox-relay` to protect transactional outbox integrity.
- Route external HTTP traffic via an NGINX Ingress Controller manifest.
- Implement hybrid network routing using `ExternalName` Services pointing to `host.docker.internal`.
- Provide automated shell scripts (`scripts/k8s-local-up.sh` and `scripts/k8s-local-down.sh`) for developer lifecycle management.

**Non-Goals:**
- Cloud infrastructure provisioning (Terraform, AWS EKS, GCP GKE, Azure AKS) — reserved for future cloud migration proposals.
- Migrating stateful data stores (PostgreSQL, Kafka, Redis, ElasticSearch) into Kubernetes.
- Creating Helm charts (Kustomize is explicitly chosen for native `kubectl` workflow).
- Automated TLS certificate provisioning via cert-manager.

## Decisions

### Decision 1: Kustomize Base and Overlay Directory Layout

We organize manifests using standard Kustomize composition:
```text
k8s/
├── base/
│   ├── kustomization.yaml
│   ├── apis/
│   │   ├── events-api.yaml
│   │   ├── reservation-api.yaml
│   │   └── search-api.yaml
│   ├── workers/
│   │   ├── events-cdc-relay.yaml
│   │   ├── search-replication.yaml
│   │   ├── res-outbox-relay.yaml
│   │   ├── res-timeout.yaml
│   │   ├── res-compensation.yaml
│   │   ├── payment.yaml
│   │   ├── refund.yaml
│   │   └── ticketing.yaml
│   ├── autoscaling/
│   │   ├── hpa-apis.yaml
│   │   └── keda-workers.yaml
│   ├── networking/
│   │   ├── ingress.yaml
│   │   └── external-services.yaml
│   └── config/
│       ├── configmap.yaml
│       └── secrets.yaml
└── overlays/
    └── local/
        ├── kustomization.yaml
        └── configmap-local-patch.yaml
```

*Rationale*: Separation of concerns allows core deployment definitions in `base/` to be reused across future staging or cloud overlays, while `overlays/local/` configures local container images (`imagePullPolicy: IfNotPresent`), replica counts, and local secret values.

### Decision 2: Hybrid Data Connectivity via ExternalName Services

Pods inside Kubernetes discover and connect to data backends using standard Kubernetes DNS names:
- `postgres-service` -> `host.docker.internal` (Port 5432)
- `kafka-service` -> `host.docker.internal` (Port 9094)
- `redis-service` -> `host.docker.internal` (Port 6379)
- `elasticsearch-service` -> `host.docker.internal` (Port 9200)
- `otel-collector-service` -> `host.docker.internal` (Port 4317)

```yaml
apiVersion: v1
kind: Service
metadata:
  name: postgres-service
spec:
  type: ExternalName
  externalName: host.docker.internal
```

*Rationale*: Decouples the application code and connection strings from physical topology. If services migrate to internal K8s statefulsets or cloud managed databases (e.g. AWS RDS/MSK) later, only the Service definition changes; zero application code or Pod container configs need updating.

### Decision 3: Worker Autoscaling Strategy & Outbox Singleton Locking

- **Kafka Workers**: Managed by KEDA `ScaledObject` pointing to the Kafka broker trigger with threshold parameters (e.g. lag threshold of 30 messages). When queue lag increases, KEDA scales worker pods from 1 to 5.
- **Outbox Relay (`tr-hw-res-outbox-relay`)**: Statically configured with `replicas: 1` and excluded from all HPA and KEDA manifests. Without distributed leader election or database row-level locking, multiple outbox relay instances could publish duplicate events from `outbox_messages`.

### Decision 4: Conservative Resource Allocations for Local Developer Machine

To prevent CPU/RAM exhaustion when running 11 pods on developer workstations:
- API Pods: `requests: { cpu: "50m", memory: "96Mi" }`, `limits: { cpu: "300m", memory: "256Mi" }`.
- Worker Pods: `requests: { cpu: "30m", memory: "64Mi" }`, `limits: { cpu: "200m", memory: "192Mi" }`.
- Total cluster request baseline: ~500m CPU and ~800Mi RAM, easily accommodated by any standard developer laptop.

## Risks / Trade-offs

- **[Risk]**: `host.docker.internal` resolution varies across Kubernetes distributions on Linux (e.g., Kind requires extra container host mapping).
  - *Mitigation*: The `scripts/k8s-local-up.sh` script detects the cluster type (Kind / Minikube / K3d / Docker Desktop) and verifies `host.docker.internal` resolution, adding a cluster CoreDNS patch or hostAlias if necessary.
- **[Risk]**: KEDA CRDs might not be installed in a vanilla local Kubernetes cluster.
  - *Mitigation*: Autoscaling manifests are decoupled: standard HPA applies natively, while KEDA manifests are isolated in `keda-workers.yaml`. The setup script checks if KEDA is installed (`kubectl get crd scaledobjects.keda.sh`) and provides guidance or applies KEDA manifests conditionally.
- **[Risk]**: Image distribution in local clusters without an external container registry.
  - *Mitigation*: The provisioning script supports direct local image caching (e.g., `kind load docker-image` or `minikube image load`) and uses `imagePullPolicy: IfNotPresent`.

## Migration Plan

1. **Prerequisite**: Ensure data infrastructure is running via `docker compose up -d` (Postgres, Redis, Kafka, ElasticSearch, OTel Collector).
2. **Build & Deploy**:
   - Execute `bash scripts/k8s-local-up.sh`.
   - The script builds Docker images for the 11 components using their respective Dockerfiles in `components/`.
   - Applies the Kustomize overlay: `kubectl apply -k k8s/overlays/local`.
   - Verifies deployment rollout: `kubectl rollout status deployment -l app.kubernetes.io/part-of=ticketreserver`.
3. **Verify Connectivity**:
   - Test Ingress endpoints (`http://localhost/events`, `http://localhost/venues`, `http://localhost/reserva/healthz`, `http://localhost/search`).
4. **Teardown**:
   - Execute `bash scripts/k8s-local-down.sh` (`kubectl delete -k k8s/overlays/local`).
