# Tasks

## 1. Base Networking, External Services, and Configuration

- [x] 1.1 Create the directory hierarchy `k8s/base/{apis,workers,autoscaling,networking,config}` and verify all directories exist
- [x] 1.2 Implement `k8s/base/networking/external-services.yaml` defining ExternalName services (`postgres-service`, `kafka-service`, `redis-service`, `elasticsearch-service`, `otel-collector-service`) pointing to `host.docker.internal` and verify with `kubectl apply --dry-run=client -f`
- [x] 1.3 Implement `k8s/base/config/configmap.yaml` and `k8s/base/config/secrets.yaml` containing standardized connection strings, Kafka broker addresses, and OTLP endpoints, verifying valid YAML syntax
- [x] 1.4 Implement `k8s/base/networking/ingress.yaml` with routing rules for `/events`, `/venues`, `/reserva`, and `/search`, verifying dry-run validation against Kubernetes Ingress schema

## 2. Web APIs Deployments and Autoscaling

- [x] 2.1 Implement Deployment and ClusterIP Service for `tr-api-events` in `k8s/base/apis/events-api.yaml` with HTTP `/healthz` probes and resource limits, verifying with `kubectl apply --dry-run=client -f`
- [x] 2.2 Implement Deployment and ClusterIP Service for `tr-api-reservation` in `k8s/base/apis/reservation-api.yaml` with HTTP `/healthz` probes and resource limits, verifying with `kubectl apply --dry-run=client -f`
- [x] 2.3 Implement Deployment and ClusterIP Service for `tr-api-search` in `k8s/base/apis/search-api.yaml` with HTTP `/healthz` probes and resource limits, verifying with `kubectl apply --dry-run=client -f`
- [x] 2.4 Implement `k8s/base/autoscaling/hpa-apis.yaml` with HPA v2 definitions targeting CPU (70%) and Memory (80%) for all 3 Web APIs, verifying with `kubectl apply --dry-run=client -f`

## 3. Background Workers Deployments and Autoscaling

- [x] 3.1 Implement Deployment manifests for `tr-hw-events-cdc-relay` and `tr-hw-search-replication` in `k8s/base/workers/`, verifying with `kubectl apply --dry-run=client -f`
- [x] 3.2 Implement Deployment manifest for Outbox Relay (`tr-hw-res-outbox-relay`) in `k8s/base/workers/res-outbox-relay.yaml` locked to `replicas: 1` with no autoscaler, verifying with `kubectl apply --dry-run=client -f`
- [x] 3.3 Implement Deployment manifests for `tr-hw-res-timeout` and `tr-hw-res-compensation` in `k8s/base/workers/`, verifying with `kubectl apply --dry-run=client -f`
- [x] 3.4 Implement Deployment manifests for `tr-hw-payment`, `tr-hw-refund`, and `tr-hw-ticketing` in `k8s/base/workers/`, verifying with `kubectl apply --dry-run=client -f`
- [x] 3.5 Implement `k8s/base/autoscaling/keda-workers.yaml` defining KEDA `ScaledObject` templates for Kafka consumers using lag triggers, verifying valid YAML structure

## 4. Kustomize Composition and Local Overlay

- [x] 4.1 Implement `k8s/base/kustomization.yaml` assembling all base resources and verify multi-document rendering via `kubectl kustomize k8s/base`
- [x] 4.2 Implement `k8s/overlays/local/kustomization.yaml` with image tags, `imagePullPolicy: IfNotPresent`, and local secrets, verifying successful rendering via `kubectl kustomize k8s/overlays/local`

## 5. Automation Scripts and Documentation

- [x] 5.1 Implement `scripts/k8s-local-up.sh` to build component images, load them into the local cluster, apply Kustomize manifests, and check rollout status, verifying execution with dry-run/syntax checks
- [x] 5.2 Implement `scripts/k8s-local-down.sh` to cleanly tear down Kubernetes resources, verifying execution with dry-run checks
- [x] 5.3 Author `k8s/README.md` with instructions for running and verifying the local cluster and update `PROJECT.md`, verifying all links and formatting

## Workflow follow-up

- Validate the complete change with `openspec validate setup-k8s-kustomize --strict`
- Present proposal and plan to user for review and proceed to implementation when requested
