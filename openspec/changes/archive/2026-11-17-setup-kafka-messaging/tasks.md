# Tasks

## 1. Configuração dos Arquivos e Scripts do Kafka

- [x] 1.1 Criar diretório `docker/kafka/` e o script `init-kafka-topics.sh` com permissão de execução, contendo a criação idempotente (`--if-not-exists`) dos 6 tópicos de domínio e 5 DLQs, e verificar sintaxe bash (`bash -n`).
- [x] 1.2 Criar `docker/kafka/docker-compose.yml` definindo os serviços `kafka` (modo KRaft, listeners 9092/9094/29093, volume persistente, healthcheck), `akhq` (porta 8085) e `kafka-init-topics` (one-shot container), garantindo que não há redes duplicadas inválidas.
- [x] 1.3 Atualizar o arquivo raiz `docker-compose.yml` adicionando `./docker/kafka/docker-compose.yml` na diretiva `include:`, e validar a configuração gerada via `docker compose config`.

## 2. Inicialização, Provisionamento e Verificação dos Tópicos

- [x] 2.1 Subir a infraestrutura com `docker compose up -d` e verificar que os containers `ticketreserver-kafka` e `ticketreserver-akhq` iniciam com status saudável (`healthy`/`running`) e que o container transitório `kafka-init-topics` finaliza com código de saída 0.
- [x] 2.2 Inspecionar os tópicos provisionados via CLI administrativa (`kafka-topics --describe`) confirmando 3 partições para os tópicos de domínio/delayed e 1 partição para as DLQs.
- [x] 2.3 Validar a política estrita de não criação automática de tópicos (`KAFKA_AUTO_CREATE_TOPICS_ENABLE: "false"`) enviando requisição para tópico não cadastrado e confirmando recusa do broker.
- [x] 2.4 Validar o funcionamento e a conectividade do painel web AKHQ através de requisição HTTP em `http://localhost:8085` verificando resposta 200 OK e listagem dos tópicos.
