#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# TicketReserver.IO - Local Kubernetes Cluster Up Script
# Builds component images, renders Kustomize overlay, and deploys to local K8s
# ==============================================================================

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$PROJECT_ROOT"

SKIP_BUILD=false
DRY_RUN=false

for arg in "$@"; do
  case $arg in
    --skip-build)
      SKIP_BUILD=true
      shift
      ;;
    --dry-run)
      DRY_RUN=true
      shift
      ;;
    --help|-h)
      echo "Usage: $0 [OPTIONS]"
      echo ""
      echo "Options:"
      echo "  --skip-build   Skip building local Docker images"
      echo "  --dry-run      Render manifests via Kustomize without applying to cluster"
      echo "  --help, -h     Show this help message"
      exit 0
      ;;
  esac
done

echo "================================================================="
echo " TicketReserver.IO - Local Kubernetes Deployment"
echo "================================================================="

# 1. Dependency Checks
command -v kubectl >/dev/null 2>&1 || { echo "[ERROR] kubectl is required but not installed. Aborting."; exit 1; }
if [ "$SKIP_BUILD" = false ]; then
  command -v docker >/dev/null 2>&1 || { echo "[ERROR] docker is required to build images. Aborting."; exit 1; }
fi

# 2. Render Check
echo "[*] Validating Kustomize overlay rendering (k8s/overlays/local)..."
kubectl kustomize k8s/overlays/local > /dev/null
echo "[OK] Kustomize overlay validated successfully."

if [ "$DRY_RUN" = true ]; then
  echo "[INFO] Dry run enabled. Printing rendered YAML summary..."
  kubectl kustomize k8s/overlays/local | grep -E "^(kind|  name):"
  echo "[OK] Dry run finished."
  exit 0
fi

# 3. Detect Cluster Context
CURRENT_CONTEXT="$(kubectl config current-context 2>/dev/null || echo "none")"
echo "[*] Current Kubernetes context: ${CURRENT_CONTEXT}"

# 4. Build Images
COMPONENTS=(
  "components/TR.API.Events:ticketreserver/tr-api-events:latest"
  "components/TR.API.Reservation:ticketreserver/tr-api-reservation:latest"
  "components/TR.API.Search:ticketreserver/tr-api-search:latest"
  "components/TR.HW.Notification:ticketreserver/tr-hw-notification:latest"
  "components/TR.HW.Repl:ticketreserver/tr-hw-repl:latest"
  "components/TR.HW.ReservationOutboxRelay:ticketreserver/tr-hw-reservation-outbox-relay:latest"
  "components/TR.HW.ResTimeout:ticketreserver/tr-hw-res-timeout:latest"
  "components/TR.HW.ResCompensator:ticketreserver/tr-hw-res-compensator:latest"
  "components/TR.HW.Payment:ticketreserver/tr-hw-payment:latest"
  "components/TR.HW.Refund:ticketreserver/tr-hw-refund:latest"
  "components/TR.HW.Ticketing:ticketreserver/tr-hw-ticketing:latest"
)

if [ "$SKIP_BUILD" = false ]; then
  echo "[*] Building 11 component Docker images..."
  for item in "${COMPONENTS[@]}"; do
    DIR="${item%%:*}"
    TAG="${item#*:}"
    echo "  -> Building ${TAG} from ${DIR}..."
    docker build -t "$TAG" "$DIR" > /dev/null
  done
  echo "[OK] All 11 images built successfully."

  # Check if Kind or Minikube or K3d needs image pre-loading
  if [[ "$CURRENT_CONTEXT" == kind-* ]]; then
    KIND_CLUSTER="${CURRENT_CONTEXT#kind-}"
    echo "[*] Detected Kind cluster '${KIND_CLUSTER}'. Pre-loading images into cluster node..."
    for item in "${COMPONENTS[@]}"; do
      TAG="${item#*:}"
      kind load docker-image "$TAG" --name "$KIND_CLUSTER"
    done
  elif [[ "$CURRENT_CONTEXT" == "minikube" ]]; then
    echo "[*] Detected Minikube. Pre-loading images..."
    for item in "${COMPONENTS[@]}"; do
      TAG="${item#*:}"
      minikube image load "$TAG"
    done
  elif [[ "$CURRENT_CONTEXT" == k3d-* ]]; then
    K3D_CLUSTER="${CURRENT_CONTEXT#k3d-}"
    echo "[*] Detected K3d cluster '${K3D_CLUSTER}'. Importing images..."
    for item in "${COMPONENTS[@]}"; do
      TAG="${item#*:}"
      k3d image import "$TAG" -c "$K3D_CLUSTER"
    done
  fi
fi

# 5. Check KEDA CRDs
if ! kubectl get crd scaledobjects.keda.sh >/dev/null 2>&1; then
  echo "[WARN] KEDA CRD (scaledobjects.keda.sh) is not installed in cluster."
  echo "       To enable event-driven autoscaling for Kafka consumers:"
  echo "       helm repo add kedacore https://kedacore.github.io/charts"
  echo "       helm install keda kedacore/keda -n keda --create-namespace"
fi

# 6. Apply Kustomize Overlay
echo "[*] Applying manifests via 'kubectl apply -k k8s/overlays/local'..."
kubectl apply -k k8s/overlays/local

# 7. Check Deployment Status
echo "[*] Checking deployment rollout status in namespace 'ticketreserver'..."
DEPLOYMENTS=$(kubectl get deployments -n ticketreserver -o jsonpath='{.items[*].metadata.name}')
for dep in $DEPLOYMENTS; do
  echo "  -> Waiting for rollout of ${dep}..."
  kubectl rollout status deployment/"$dep" -n ticketreserver --timeout=60s || true
done

echo ""
echo "================================================================="
echo "[SUCCESS] TicketReserver.IO local cluster deployed successfully!"
echo "================================================================="
kubectl get pods,services,ingress -n ticketreserver
