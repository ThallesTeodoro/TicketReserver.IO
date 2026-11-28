# TicketReserver.IO - Kubernetes Manifests (Kustomize)

Este diretório contém a especificação declarativa de orquestração para os 11 componentes do **TicketReserver.IO** gerenciados via **Kustomize**, com suporte a autoscaling horizontal (HPA e KEDA), Ingress HTTP e conectividade híbrida com a infraestrutura de dados local.

---

## 1. Topologia e Arquitetura Híbrida

A arquitetura adota um modelo híbrido para o ambiente de desenvolvimento e testes de concorrência:
1. **Camada de Aplicação (Kubernetes):**
   - **3 Web APIs:** `events-api`, `reservation-api`, `search-api` (expostas via Ingress NGINX e ClusterIP).
   - **8 Heavy Workers:** `events-cdc-relay`, `search-replication-worker`, `res-outbox-relay`, `res-timeout-worker`, `res-compensation-worker`, `payment-worker`, `refund-worker`, `ticketing-worker`.
2. **Camada de Infraestrutura de Dados (Docker Compose no Host):**
   - PostgreSQL (port 5432)
   - Apache Kafka (port 9094 - listener externo)
   - Redis (port 6379)
   - ElasticSearch (port 9200)
   - OpenTelemetry Collector (port 4317 gRPC / 4318 HTTP)
3. **Bridge de Resolução Híbrida (`ExternalName` Services):**
   - Os Pods dentro do Kubernetes se conectam a `postgres-service`, `kafka-service`, `redis-service`, `elasticsearch-service` e `otel-collector-service`.
   - Cada um desses serviços é do tipo `ExternalName`, resolvendo diretamente para `host.docker.internal`.

---

## 2. Estrutura de Diretórios Kustomize

```text
k8s/
├── README.md                           # Documentação de operações e deployment
├── base/                               # Manifestos agnósticos de ambiente
│   ├── kustomization.yaml              # Agregador dos recursos base
│   ├── apis/                           # Deployments e ClusterIP Services das Web APIs
│   │   ├── events-api.yaml
│   │   ├── reservation-api.yaml
│   │   └── search-api.yaml
│   ├── autoscaling/                    # Políticas de escalabilidade
│   │   ├── hpa-apis.yaml               # HPA v2 baseado em CPU (70%) e RAM (80%)
│   │   └── keda-workers.yaml           # KEDA ScaledObjects baseados em lag do Kafka
│   ├── config/                         # Variáveis de ambiente e segredos
│   │   ├── configmap.yaml
│   │   └── secrets.yaml
│   ├── networking/                     # Entrada de tráfego e pontes externas
│   │   ├── external-services.yaml      # ExternalName -> host.docker.internal
│   │   └── ingress.yaml                # Ingress NGINX Controller (/events, /reserva, /search)
│   └── workers/                        # Deployments dos workers em background
│       ├── events-cdc-relay.yaml
│       ├── payment.yaml
│       ├── refund.yaml
│       ├── res-compensation.yaml
│       ├── res-outbox-relay.yaml       # Singleton (réplica = 1 estrito, sem HPA)
│       ├── res-timeout.yaml
│       ├── search-replication.yaml
│       └── ticketing.yaml
└── overlays/
    └── local/                          # Overlay para clusters locais (Kind, Minikube, K3d)
        ├── kustomization.yaml          # Configura namespace, labels e tags locais
        └── namespace.yaml              # Namespace 'ticketreserver'
```

---

## 3. Pré-requisitos

1. **Docker & Docker Compose** (para a infraestrutura de dados).
2. **Kubectl** (v1.28+ com Kustomize v5 nativo).
3. **Cluster Local:**
   - [Kind](https://kind.sigs.k8s.io/) (`kind create cluster --name ticketreserver`)
   - [Minikube](https://minikube.sigs.k8s.io/) (`minikube start`)
   - [K3d](https://k3d.io/) (`k3d cluster create ticketreserver`)
   - Ou Docker Desktop Kubernetes ativo.
4. *(Opcional)* **KEDA** (para autoscaling dos workers por lag de tópicos Kafka):
   ```bash
   helm repo add kedacore https://kedacore.github.io/charts
   helm install keda kedacore/keda -n keda --create-namespace
   ```

---

## 4. Guia de Execução Rápida

### 4.1. Iniciar Infraestrutura de Dados (Docker Compose)
No diretório raiz do projeto:
```bash
docker compose up -d postgres redis kafka elasticsearch otel-collector
```

### 4.2. Validar Renderização dos Manifestos (Dry Run)
```bash
./scripts/k8s-local-up.sh --dry-run
```
Ou diretamente via `kubectl`:
```bash
kubectl kustomize k8s/overlays/local
```

### 4.3. Construir Imagens e Fazer o Deploy no Cluster
```bash
./scripts/k8s-local-up.sh
```
*Dica: caso já tenha construído as imagens Docker e queira apenas atualizar os manifestos:*
```bash
./scripts/k8s-local-up.sh --skip-build
```

### 4.4. Verificar Estado dos Pods e Serviços
```bash
kubectl get pods,services,ingress,hpa -n ticketreserver
```

### 4.5. Teardown / Limpeza
Para remover todos os recursos criados no cluster:
```bash
./scripts/k8s-local-down.sh
```

---

## 5. Estratégia de Autoscaling

- **Web APIs:** Escalabilidade via Horizontal Pod Autoscaler padrão (`HPA v2`), configurado para manter a utilização média de CPU abaixo de 70% e memória abaixo de 80%, variando entre 1 e 5 réplicas.
- **Background Workers:** Escalabilidade event-driven via `KEDA` monitorando o consumer lag de cada tópico específico do Kafka (ex: `reservation.v1.checkout_req`, `payment.v1.approved`). Quando o acúmulo de mensagens ultrapassa o threshold, o KEDA escala os workers de 1 a 5 pods.
- **Outbox Relay (`res-outbox-relay`):** Política estrita de **Singleton (`replicas: 1`)** sem nenhum autoscaler configurado, garantindo atomicidade no consumo da tabela de outbox transacional sem concorrência destrutiva.
