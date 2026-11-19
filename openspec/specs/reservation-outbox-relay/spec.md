# reservation-outbox-relay Specification

## Purpose

Prover o despacho contínuo, resiliente e transacional de mensagens retidas na tabela outbox do banco de reservas para os tópicos particionados do Apache Kafka, garantindo entrega At-Least-Once, ordenação estrita por reserva e mitigação de falhas com Dead Letter Queue (DLQ).

## Requirements

### Requirement: Leitura Transacional Concorrente com Bloqueio Não-Bloqueante
O serviço de despacho SHALL consultar periodicamente lotes ordenados de mensagens pendentes na tabela `outbox_messages` do `reservation_db` com `deliver_after <= NOW()`, utilizando bloqueio a nível de linha `FOR UPDATE SKIP LOCKED` para permitir execução concorrente e escalabilidade horizontal sem contenção.

#### Scenario: Drenagem de mensagens pendentes elegíveis
- **WHEN** existem registros com status `PENDING` e `deliver_after` menor ou igual ao horário atual UTC
- **THEN** o worker seleciona um lote de até N mensagens ordenadas por `created_at` ascendente sem bloquear outras instâncias que consultem a tabela simultaneamente

#### Scenario: Ausência de mensagens elegíveis para entrega
- **WHEN** todas as mensagens pendentes possuem `deliver_after` no futuro ou a tabela está vazia
- **THEN** o worker não seleciona nenhum registro e aplica uma pausa adaptativa antes do próximo ciclo de consulta

### Requirement: Roteamento de Mensagens e Particionamento por Reserva
O serviço de despacho SHALL publicar os eventos no Apache Kafka roteando para o tópico adequado com base no tipo de evento (`event_type`), definindo a chave da mensagem (`Message.Key`) como o identificador do agregado (`aggregate_id`) para garantir ordem estrita de entrega por reserva.

#### Scenario: Publicação de evento imediato de checkout
- **WHEN** uma mensagem com `event_type = 'reservation.checkout_req'` é processada
- **THEN** o evento é publicado no tópico `reservation.v1.checkout_req` com partition key correspondente ao `aggregate_id` da reserva e headers de metadados preenchidos

#### Scenario: Publicação de evento temporizado de retenção de assento
- **WHEN** uma mensagem com `event_type = 'reservation.hold_created'` atinge seu tempo de entrega
- **THEN** o evento é publicado no tópico `reservation.v1.hold_created.delayed` preservando a mesma partition key por `aggregate_id`

### Requirement: Confirmação Transacional de Entrega At-Least-Once
O serviço de despacho SHALL atualizar o estado da mensagem na tabela outbox para `PUBLISHED` e registrar `processed_at` exclusivamente após receber a confirmação de persistência (ACK) emitida pelo broker Kafka sob garantia `Acks = All`.

#### Scenario: Publicação confirmada com sucesso pelo broker
- **WHEN** o broker Kafka confirma a gravação da mensagem na partição
- **THEN** o worker atualiza o status do registro para `PUBLISHED`, limpa mensagens de erro anteriores e persiste o carimbo de data/hora de conclusão

### Requirement: Resiliência a Falhas Transitórias e Encaminhamento para DLQ
O serviço de despacho SHALL aplicar retry com backoff exponencial diante de falhas de comunicação com o Kafka e, ao atingir o limite configurado de tentativas consecutivas, transicionar a mensagem para `FAILED` e encaminhá-la à Dead Letter Queue correspondente.

#### Scenario: Falha transitória de comunicação com o Kafka
- **WHEN** a tentativa de publicação no Kafka falha por indisponibilidade transitória ou timeout de rede
- **THEN** o worker incrementa `retry_count`, registra o erro e recalcula `deliver_after` com backoff exponencial, mantendo o status em `PENDING`

#### Scenario: Esgotamento de tentativas de entrega
- **WHEN** uma mensagem atinge o número máximo de retentativas permitidas
- **THEN** o worker altera o status para `FAILED`, publica o registro no tópico `reservation.v1.checkout_req.DLQ` e registra alerta crítico em log estruturado
