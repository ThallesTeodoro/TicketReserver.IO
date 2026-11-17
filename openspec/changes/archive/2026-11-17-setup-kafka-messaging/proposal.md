# Proposal: Infraestrutura Central de Mensageria com Apache Kafka (KRaft) e AKHQ

## Why

O ecossistema **TicketReserver.IO** adota Arquitetura Orientada a Eventos (EDA) para orquestrar o ciclo de vida de alta concorrência de reservas, pagamento e emissão de ingressos com consistência eventual e compensação (Sagas). Para suportar o despacho do Transactional Outbox e a comunicação desacoplada entre os microsserviços (`SVC_RESERVA`, `SVC_PAGAMENTO`, `SVC_TICKETING`), é indispensável disponibilizar a infraestrutura central de mensageria local via Docker Compose com Apache Kafka moderno (modo KRaft, sem dependência do ZooKeeper) e interface visual de monitoramento via AKHQ.

## What Changes

- Adição de configuração Docker Compose modular em `docker/kafka/docker-compose.yml` para os serviços `kafka`, `akhq` e `kafka-init-topics`.
- Inclusão do módulo de mensageria no `docker-compose.yml` raiz da aplicação via diretiva `include:`.
- Configuração do Apache Kafka no modo KRaft (Kafka Raft Metadata Mode) eliminando ZooKeeper, com listeners segregados para containers internos (`PLAINTEXT://kafka:9092`), tráfego do quórum de controle (`CONTROLLER://kafka:29093`) e acesso externo a partir do host de desenvolvimento (`PLAINTEXT_HOST://localhost:9094`).
- Desativação de autocriação de tópicos (`KAFKA_AUTO_CREATE_TOPICS_ENABLE: "false"`) para assegurar controle estrito de particionamento e políticas de retenção.
- Script de inicialização idempotente (`docker/kafka/init-kafka-topics.sh`) executado pelo serviço transitório `kafka-init-topics` para provisionamento dos tópicos de domínio (`reservation.v1.checkout_req`, `payment.v1.approved`, `payment.v1.refunded`, `ticketing.v1.issued`, `ticketing.v1.failed`), tópico especial de reconciliação de timeout de 10 minutos (`reservation.v1.hold_created.delayed`) com 3 partições, e as respectivas Dead Letter Queues (DLQs) com 1 partição.
- Integração da ferramenta web AKHQ na porta `8085` mapeada no host para inspeção visual de mensagens, tópicos, consumer groups e lag.

## Capabilities

### New Capabilities
- `infra-kafka-messaging`: Infraestrutura de streaming e mensageria distribuída baseada em Apache Kafka (KRaft) e console AKHQ, com listeners segregados, tópicos de negócio e DLQs provisionados deterministicamente.

### Modified Capabilities
<!-- Nenhuma capability existente tem seus requisitos alterados nesta mudança de infraestrutura. -->

## Impact

- **Infraestrutura Docker:** Novo módulo conteinerizado adicionado à rede `ticketreserver-internal-net` expondo as portas `9094` (Kafka broker host) e `8085` (AKHQ web).
- **Dependências futuras:** Habilita a implementação do `TR.HW.OutboxRelay` e de todos os consumidores de eventos (`Payment`, `Ticketing`, `Refund`, `ResTimeout`).
- **Recursos da máquina:** Consumo adicional aproximado de ~500MB de RAM para o Kafka KRaft e ~150MB para o AKHQ.
