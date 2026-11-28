#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# TicketReserver.IO - Local Kubernetes Cluster Down Script
# Gracefully tears down all resources deployed via Kustomize local overlay
# ==============================================================================

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$PROJECT_ROOT"

DRY_RUN=false

for arg in "$@"; do
  case $arg in
    --dry-run)
      DRY_RUN=true
      shift
      ;;
    --help|-h)
      echo "Usage: $0 [OPTIONS]"
      echo ""
      echo "Options:"
      echo "  --dry-run      Print resources to be deleted without removing them"
      echo "  --help, -h     Show this help message"
      exit 0
      ;;
  esac
done

echo "================================================================="
echo " TicketReserver.IO - Local Kubernetes Teardown"
echo "================================================================="

command -v kubectl >/dev/null 2>&1 || { echo "[ERROR] kubectl is required but not installed. Aborting."; exit 1; }

if [ "$DRY_RUN" = true ]; then
  echo "[INFO] Dry run enabled. Printing resources that would be deleted:"
  kubectl kustomize k8s/overlays/local | grep -E "^(kind|  name):"
  echo "[OK] Dry run finished."
  exit 0
fi

echo "[*] Deleting all resources deployed via 'kubectl delete -k k8s/overlays/local'..."
kubectl delete -k k8s/overlays/local --ignore-not-found=true

echo "[OK] Teardown completed."
