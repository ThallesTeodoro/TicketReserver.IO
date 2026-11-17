# Design: Infraestrutura Central de Mensageria (Kafka KRaft & AKHQ)

## Context

O ambiente de desenvolvimento local já possui PostgreSQL (`catalog_events_db`) e Redis operando de forma conteinerizada e modular. Para suportar o padrão Transactional Outbox e a arquitetura orientada a eventos especificada em `arch-decisions.md` e `infra-spec.md`, precisamos adicionar o barramento central de mensageria com Apache Kafka e interface visual AKHQ sem acoplamento a orquestradores de metadados legados (ZooKeeper).

## Goals / Non-Goals

**Goals:**
- Prover um broker Apache Kafka 7.6+ em modo KRaft com consumo eficiente de recursos em desenvolvimento (~512MB RAM).
- Expor múltiplos listeners para permitir tráfego simultâneo na rede interna Docker (`kafka:9092`) e a partir da máquina host (`localhost:9094`).
- Automatizar o provisionamento idempotente dos 6 tópicos de domínio e 5 DLQs através de um serviço container transitório (`kafka-init-topics`).
- Prover a interface visual AKHQ acessível na porta `8085` do host.
- Integrar a mensageria ao `docker-compose.yml` raiz através do padrão modular `include: ./docker/kafka/docker-compose.yml`.

**Non-Goals:**
- Configuração de cluster multi-broker com tolerância a falhas para produção (o escopo atual é nó único otimizado para o ambiente de engenharia local).
- Implementação de Schema Registry ou Confluent Control Center (o sistema utilizará contratos JSON/Protobuf diretos no payload dos eventos).
- Configuração do conector CDC Debezium ou Elasticsearch (postergados deliberadamente para manter foco no core de concorrência).

## Decisions

### 1. Kafka em Modo KRaft com Imagem `confluentinc/cp-kafka:7.6.0`
* **Decisão:** Utilizar `confluentinc/cp-kafka:7.6.0` configurado como `broker,controller` com um `CLUSTER_ID` fixo em Base64 (`MkU3OEVBNTcwNTJENDM2Qk`).
* **Racional:** Elimina a necessidade de um container ZooKeeper separado, reduzindo consumo de memória, tempo de boot para < 10 segundos e simplificando o lifecycle no Docker Compose.
* **Alternativas consideradas:**
  * *Apache Kafka com ZooKeeper:* Rejeitado por aumentar footprint de memória e complexidade operacional desnecessária.
  * *Bitnami Kafka:* Boa alternativa, porém a imagem Confluent possui utilitários CLI nativos (`kafka-topics`, etc.) mais integrados para os scripts de inicialização.

### 2. Arquitetura de Listeners e Portas
* **Decisão:** Configurar três listeners dedicados no broker:
  * `PLAINTEXT://0.0.0.0:9092` (advertised como `kafka:9092` na rede `ticketreserver-internal-net`).
  * `PLAINTEXT_HOST://0.0.0.0:9094` (advertised como `localhost:9094` para clientes no host).
  * `CONTROLLER://0.0.0.0:29093` (utilizado exclusivamente para comunicação do quórum KRaft).
* **Racional:** Permite que serviços conteinerizados acessem o broker via DNS interno sem colidir com portas do host, ao mesmo tempo em que ferramentas de desenvolvimento locais (CLI, IDE, linters) consigam se conectar em `localhost:9094`.

### 3. Provisionador de Tópicos Idempotente (`kafka-init-topics`)
* **Decisão:** Criar um serviço no Compose que utiliza a mesma imagem do Kafka, monta o script `docker/kafka/init-kafka-topics.sh`, aguarda a disponibilidade do broker (`cub kafka-ready` ou loop com `kafka-broker-api-versions`), executa comandos `kafka-topics --create --if-not-exists` e encerra com `restart: "no"`.
* **Racional:** Garante que tópicos críticos e DLQs sempre existam com o particionamento correto (3 partições para concorrência de workers e 1 partição para DLQs ordenadas) antes que qualquer aplicação tente publicar, mesmo em ambientes limpos de CI/CD.
* **Alternativas consideradas:**
  * *`KAFKA_AUTO_CREATE_TOPICS_ENABLE: "true"`:* Rejeitado porque criaria tópicos com partição default (1 partição), quebrando a capacidade de paralelismo dos consumidores.

### 4. AKHQ como Console Web de Mensageria
* **Decisão:** Adicionar o serviço `akhq` com a imagem `tchiotludo/akhq:latest`, mapeando a porta host `8085:8080` e configurando bootstrap server para `kafka:9092`.
* **Racional:** Interface leve (< 150MB RAM), sem necessidade de banco relacional para persistir estado, com suporte nativo a visualização de mensagens, lag de consumer groups e tópicos.

## Risks / Trade-offs

- **[Risco: Consumo excessivo de memória em máquinas de desenvolvimento]**
  * *Mitigação:* Configurar `KAFKA_HEAP_OPTS: "-Xms512m -Xmx512m"` no container do Kafka.
- **[Risco: Concorrência na inicialização entre Kafka e script de tópicos]**
  * *Mitigação:* O serviço `kafka-init-topics` terá `depends_on` com `condition: service_healthy` do broker Kafka e laço de tolerância em shell script.
- **[Risco: Perda de mensagens e tópicos ao reiniciar containers]**
  * *Mitigação:* Montagem de volume nomeado persistente `kafka_data` mapeado em `/var/lib/kafka/data`.
