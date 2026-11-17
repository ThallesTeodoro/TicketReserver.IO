# Spec Delta

## Purpose

Fornecer a infraestrutura central de mensageria e streaming distribuído utilizando Apache Kafka em modo KRaft com gerenciamento visual via AKHQ e provisionamento determinístico de tópicos para orquestração de eventos no ecossistema TicketReserver.IO.

## ADDED Requirements

### Requirement: Kafka Broker em Modo KRaft com Listeners Segregados
O sistema SHALL provisionar uma instância única de Apache Kafka operando exclusivamente em modo KRaft (sem ZooKeeper), segregando listeners de comunicação interna entre containers, quórum de controle de metadados e acesso externo para o host de desenvolvimento.

#### Scenario: Conexão interna entre microsserviços
- **WHEN** um serviço ou worker na rede `ticketreserver-internal-net` conecta em `kafka:9092`
- **THEN** o broker aceita conexões usando protocolo PLAINTEXT interno sem autenticação

#### Scenario: Conexão a partir da máquina host
- **WHEN** ferramentas de debug, testes ou desenvolvedores conectam em `localhost:9094`
- **THEN** o broker aceita conexões roteando para o listener PLAINTEXT_HOST

#### Scenario: Inicialização do cluster sem ZooKeeper
- **WHEN** o container do Kafka é iniciado
- **THEN** o cluster é inicializado com storage formatado via CLUSTER_ID persistido e processo assumindo papéis de broker e controller

### Requirement: Desativação de Autocriação de Tópicos
O broker SHALL rejeitar a criação dinâmica/automática de tópicos por produtores ou consumidores, garantindo conformidade estrita com a topologia planejada.

#### Scenario: Produtor publica em tópico inexistente
- **WHEN** um cliente tenta publicar mensagem em um tópico não provisionado previamente
- **THEN** o broker retorna erro `UNKNOWN_TOPIC_OR_PARTITION` em vez de criar o tópico com partições default

### Requirement: Provisionamento Determinístico de Tópicos e DLQs
O sistema SHALL executar uma rotina automatizada e idempotente de inicialização que provisiona todos os tópicos de domínio, tópicos com delay de reconciliação e Dead Letter Queues (DLQs) obrigatórias com os respectivos fatores de partição.

#### Scenario: Execução da rotina de inicialização de tópicos
- **WHEN** o container do Kafka atinge estado saudável (`healthy`)
- **THEN** os tópicos de negócio (`reservation.v1.checkout_req`, `payment.v1.approved`, `payment.v1.refunded`, `ticketing.v1.issued`, `ticketing.v1.failed`) e o tópico delayed (`reservation.v1.hold_created.delayed`) são criados com 3 partições e replication-factor 1
- **THEN** os tópicos de DLQ (`reservation.v1.checkout_req.DLQ`, `payment.v1.approved.DLQ`, `payment.v1.refunded.DLQ`, `ticketing.v1.issued.DLQ`, `ticketing.v1.failed.DLQ`) são criados com 1 partição e replication-factor 1

#### Scenario: Idempotência de inicialização em reinicializações
- **WHEN** o ambiente Docker é reiniciado e os tópicos já existem no storage persistido
- **THEN** o script de criação detecta a existência prévia sem gerar erros de interrupção ou falhas nos containers

### Requirement: Interface Web AKHQ para Monitoramento
O sistema SHALL disponibilizar uma interface web do AKHQ integrada ao cluster Kafka local, permitindo inspeção de tópicos, mensagens e consumer groups.

#### Scenario: Acesso à interface web do AKHQ
- **WHEN** um desenvolvedor acessa `http://localhost:8085` no navegador
- **THEN** a interface web do AKHQ é exibida exibindo a lista de tópicos provisionados, configurações de partição e métricas de armazenamento
