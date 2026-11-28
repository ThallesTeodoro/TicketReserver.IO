# Spec Delta

## Purpose

Provides declarative Kubernetes manifests structured with Kustomize for deploying, autoscaling, and managing all 11 TicketReserver.IO components (3 Web APIs and 8 Workers) on a local cluster with Ingress routing and hybrid ExternalName connectivity to existing data services.

## ADDED Requirements

### Requirement: Kustomize Base and Local Overlay Structure
The system SHALL organize Kubernetes resources into a common `base/` directory and a local development overlay `overlays/local/`, such that all manifests can be validated and rendered into valid Kubernetes multi-document YAML via `kubectl kustomize`.

#### Scenario: Rendering local overlay manifests
- **WHEN** the command `kubectl kustomize k8s/overlays/local` is executed
- **THEN** Kustomize renders all application Deployments, Services, Ingress, HPAs, KEDA ScaledObjects, ConfigMaps, and Secrets without syntax or reference errors

### Requirement: Declarative Deployments for Web APIs
The system SHALL provide Kubernetes Deployment manifests and ClusterIP Services for the 3 Web APIs (`tr-api-events`, `tr-api-reservation`, `tr-api-search`) with explicit CPU/RAM resource requests and limits, container port 8080, and HTTP readiness/liveness probes pointing to `/healthz`.

#### Scenario: API Pod health and service discovery
- **WHEN** an API Deployment is applied to the cluster
- **THEN** Kubernetes creates Pods with configured health probes and exposes them internally through a ClusterIP Service on port 8080

### Requirement: Declarative Deployments for Background Workers and Outbox Singleton
The system SHALL provide Kubernetes Deployment manifests for all 8 background workers with resource limits, environment variable bindings from ConfigMaps and Secrets, and SHALL enforce an immutable single-replica constraint (replica count = 1) for `tr-hw-res-outbox-relay`.

#### Scenario: Worker deployment execution
- **WHEN** the 8 background worker Deployments are applied to the cluster
- **THEN** each worker runs with defined CPU/Memory constraints, and `tr-hw-res-outbox-relay` is locked to exactly one replica without any autoscaling definition

### Requirement: Horizontal Pod Autoscaler and KEDA Autoscaling Policies
The system SHALL provide Kubernetes HorizontalPodAutoscaler (HPA v2) manifests for Web APIs scaling between 1 and 5 replicas based on CPU (70%) and Memory (80%) utilization, and KEDA ScaledObject manifests for Kafka-consuming workers scaling on consumer group lag thresholds.

#### Scenario: Web API autoscaling specification
- **WHEN** an HPA manifest for a Web API is inspected
- **THEN** it defines `minReplicas: 1`, `maxReplicas: 5`, and targets average CPU utilization of 70% and Memory utilization of 80%

#### Scenario: Worker event-driven autoscaling specification
- **WHEN** a KEDA ScaledObject manifest for a Kafka consumer worker is inspected
- **THEN** it targets the appropriate Kafka consumer group with a lag threshold trigger, scaling replicas between 1 and 5

### Requirement: Ingress HTTP Routing
The system SHALL provide a Kubernetes Ingress manifest compatible with NGINX Ingress Controller that routes incoming HTTP traffic on port 80 for paths `/events` and `/venues` to `tr-api-events`, `/reserva` to `tr-api-reservation`, `/search` to `tr-api-search`, and `/healthz` to gateway status.

#### Scenario: Routing external requests through Ingress
- **WHEN** an HTTP request matching `/events`, `/venues`, `/reserva`, or `/search` reaches the cluster Ingress
- **THEN** the Ingress controller proxies the request to the corresponding internal API Service

### Requirement: Hybrid Data Infrastructure Connectivity via ExternalName Services
The system SHALL provide Kubernetes `ExternalName` Services that route internal cluster hostnames (`postgres-service`, `kafka-service`, `redis-service`, `elasticsearch-service`, `otel-collector-service`) to `host.docker.internal` on their respective standard ports.

#### Scenario: Resolving external data services from Pods
- **WHEN** an application Pod connects to `postgres-service:5432`, `kafka-service:9094`, `redis-service:6379`, `elasticsearch-service:9200`, or `otel-collector-service:4317`
- **THEN** Kubernetes DNS resolves the hostname to `host.docker.internal`, routing traffic to the Docker Compose data infrastructure running on the host

### Requirement: Declarative Configuration and Secret Management
The system SHALL provide ConfigMaps for non-sensitive application settings (service URLs, Kafka bootstrap servers, OTLP endpoint, log levels) and Secrets for sensitive credentials (database passwords, Redis credentials), mounted into containers as environment variables.

#### Scenario: Environment injection from ConfigMaps and Secrets
- **WHEN** any API or Worker Pod starts in the cluster
- **THEN** its container environment variables are populated from the centralized ConfigMap and Secret keys
