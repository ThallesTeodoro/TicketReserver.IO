# Diretrizes do Projeto - Sistema de Reserva de Ingressos (TicketReserver.IO)

Este documento define as diretrizes de arquitetura, padrões de design, convenções de código e stack tecnológica para o desenvolvimento do sistema de reservas de ingressos de alta concorrência da plataforma **TicketReserver.IO**.

O sistema é orientado a eventos (Event-Driven Architecture), baseado em **Clean Architecture**, **CQRS Lite Nativo** e microsserviços distribuídos, com foco em resiliência extrema, consistência eventual e eliminação de contenção de inventário (Seat Hold distribuído).

---

## 1. Visão Geral da Arquitetura

O ecossistema é composto por microsserviços e workers independentes operando sobre Apache Kafka e PostgreSQL. Cada microsserviço adota internamente **Clean Architecture** combinada com **Vertical Slices** na camada de Aplicação.

### 1.1. Camadas da Solução (Clean Architecture)
A solução possui isolamento estrito entre quatro camadas e a suíte de testes:
*   **Domain:** O coração da aplicação. Contém Entidades ricas, Value Objects, Enums e Exceções de Domínio. Não possui dependências de pacotes externos além do BCL do .NET (zero referências a ORMs ou frameworks).
*   **Application:** Casos de uso organizados em *Vertical Slices* (`Application/{Aggregate}/{UseCase}/`), contendo Commands, Queries, Handlers nativos, validadores FluentValidation, mapeamentos estáticos e contratos/portas de infraestrutura (`IRepository`, `IUnitOfWork`, `IMessagePublisher`).
*   **Infrastructure:** Implementação das portas. Persistência híbrida via EF Core e Dapper, adaptadores de mensageria (Kafka/Confluent.Kafka), Redis e clientes externos.
*   **WebApi:** Ponto de entrada HTTP utilizando **Carter** (`CarterModule`), contratos de requisição/resposta, envelope padronizado `JsonResponse<TData, TError>` e middleware global de exceções.
*   **UnitTests:** Testes unitários com `xUnit`, `Moq` e `FluentAssertions` seguindo o padrão AAA.

### 1.2. CQRS Lite Nativo (Sem MediatR)
*   **Sem Bibliotecas Pesadas:** É expressamente proibido o uso de `MediatR` ou `AutoMapper`.
*   **Contratos Próprios:** Utilizamos interfaces enxutas (`ICommand<TResponse>`, `ICommandHandler<TCommand, TResponse>`, `IQuery<TResponse>`, `IQueryHandler<TQuery, TResponse>`).
*   **Registro por DI:** Os handlers são registrados via Assembly Scan nativo ou extensão determinística no container de DI.
*   **Mapeamento Explícito:** Mapeamentos entre Entidades e DTOs/Responses são implementados exclusivamente através de métodos de extensão estáticos (`{Entity}MappingExtensions.cs`).

---

## 2. Stack Tecnológica (.NET Ecosystem)

*   **Linguagem & Runtime:** C# 12 / .NET 8 (com `<Nullable>enable</Nullable>`).
*   **API & Endpoints:** ASP.NET Core Minimal APIs gerenciadas via **Carter** (`CarterModule`).
*   **CQRS:** CQRS Lite nativo em C# (sem dependências externas).
*   **Validação de Entrada:** FluentValidation (validação de contratos HTTP e formato sintático).
*   **Persistência Relacional:**
    *   **EF Core (PostgreSQL / Npgsql):** Para comandos, transações de escrita e Unit of Work (mapeamento 100% via Fluent API).
    *   **Dapper:** Para consultas de leitura de alta performance e paginação rápida (estritamente parametrizadas).
*   **Cache & Lock Distribuído:** StackExchange.Redis (Seat Hold via `SET NX EX`).
*   **Mensageria:** Apache Kafka via `Confluent.Kafka` ou abstração compatível com Outbox/Inbox resiliente.
*   **Busca:** ElasticSearch sincronizado via Debezium (CDC) e Worker de Replicação.
*   **Resiliência & Políticas:** Polly (Retry com Exponential Backoff e Circuit Breaker).
*   **Telemetria:** OpenTelemetry (Traces com W3C `traceparent`, Metrics) com Serilog e OTLP.
*   **Testes:** xUnit, Moq, FluentAssertions e Bogus.

---

## 3. Topologia de Microsserviços e Fluxos (System Design)

Conforme formalizado nas decisões arquiteturais (`arch-decisions.md`):

### 3.1. Microsserviços e Domínios
1. **Catálogo de Eventos (`SVC_EVENTOS`):** API de Eventos (`TR.API.Events`), Redis Cache, Eventos DB (PostgreSQL) e Debezium (CDC) enviando eventos para o Kafka.
2. **Busca (`SVC_BUSCA`):** API Search (TR.API.Search) sobre ElasticSearch, atualizada pelo worker de replicação que consome o CDC do Kafka.
3. **Reserva (`SVC_RESERVA`):**
   * **API de Reserva (`TR.API.Reservation`):** Endpoints `/hold` (garantia de assento temporária) e `/checkout` (envio para pagamento).
   * **Worker OutboxRelay:** Lê tabela Outbox do PostgreSQL e publica mensagens no Kafka.
   * **Worker Timeout (`TR.HW.ResTimeout`):** Consome de tópicos com atraso (`Kafka_Delayed`) e cancela/expira reservas não pagas após 10 minutos.
   * **Worker de Compensação:** Atualiza reserva para `CANCELLED` em caso de estorno.
4. **Pagamento (`SVC_PAGAMENTO`):**
   * **Worker Pagamento (`TR.HW.Payment`):** Consome `reservation.checkout_req`, processa transação e publica `payment.approved`.
   * **Worker Estorno Automático (`TR.HW.Refund`):** Consome `ticketing.failed`, invoca o Gateway de Pagamento para Void/Refund, marca transação como `REFUNDED` e publica `payment.refunded`.
5. **Emissão de Ingressos (`SVC_TICKETING`):**
   * **Worker Ticketing (`TR.HW.Ticketing`):** Consome `payment.approved` e persiste o ingresso. Em caso de falha de concorrência, emite `ticketing.failed`.
6. **Notificação (`SVC_NOTIFICACAO`):** Worker para envio de confirmações e avisos de estorno por e-mail/push.

### 3.2. Padrões de Resiliência e Concorrência
*   **Seat Hold (Lock em Memória):** `/hold` realiza lock atômico no Redis (`SET seat:{id} NX EX 600`) antes de qualquer gravação no Postgres.
*   **Transactional Outbox:** Toda alteração de estado no Postgres grava a mensagem na tabela `outbox_messages` na mesma transação atômica. O `OutboxRelay` despacha os eventos para o Kafka.
*   **Delayed Messaging (Timeout 10 min):** O evento `reservation.hold_created` é roteado para tópicos Kafka de atraso/TTL, entregando a mensagem ao `TR.HW.ResTimeout` após 10 minutos para reconciliação.
*   **Saga de Compensação e Auto-Refund:** Falhas na emissão (`ticketing.failed`) desencadeiam estorno imediato no Gateway de Pagamento (`TR.HW.Refund`), publicação de `payment.refunded`, cancelamento da reserva e notificação do cliente.

---

## 4. Estrutura de Solução e Projetos

A nomenclatura padrão segue o prefixo do produto (`TicketReserver` ou `TR.`):

```text
TicketReserver.Reservation/
├── source/
│   ├── TicketReserver.Reservation.Domain/            # Entidades ricas, Value Objects, Enums, Exceptions
│   │   ├── Contracts/Persistence/                    # IRepository<T>, IUnitOfWork
│   │   └── Entities/                                 # Entidades com invariantes e IReadOnlyCollection
│   ├── TicketReserver.Reservation.Application/       # Casos de uso em Vertical Slices
│   │   ├── Common/Messaging/                         # ICommand, IQuery, ICommandHandler, IQueryHandler
│   │   └── {Aggregate}/{UseCase}/                    # Command/Query, Handler, Validator, Mappings
│   ├── TicketReserver.Reservation.Infrastructure/    # EF Core, Repositories, Dapper, Kafka, Redis
│   │   ├── Messaging/                                # Producers, Consumers resilientes, OutboxPublisher
│   │   └── Persistence/                              # DbContext, Configurations (Fluent API), Repositories
│   └── TicketReserver.Reservation.WebApi/            # CarterModules, Program.cs, JsonResponse, Middlewares
│       ├── Contracts/                                # JsonResponse<TData, TError>
│       ├── Endpoints/                                # CarterModules por agregado
│       └── Middlewares/                              # ExceptionHandlerMiddleware global
└── tests/
    └── TicketReserver.Reservation.UnitTests/         # Testes AAA com xUnit, Moq e FluentAssertions
        └── Application/{Aggregate}/{UseCase}Tests.cs
```

---

## 5. Diretrizes de Desenvolvimento (Regras de Ouro & Hard Blockers)

1. **Domínio Rico e Invariantes:**
   * Entidades de domínio não podem ser anêmicas. Construtores ou métodos de fábrica estáticos (`Create`) devem validar as regras de negócio essenciais antes da instanciação.
   * Propriedades devem usar `private set` ou `init`. `public set` é proibido sem justificativa documentada.
   * Propriedades de navegação/coleções DEVEM ser expostas como `IReadOnlyCollection<T>` com campo privado de apoio (`private readonly List<T> _items = new();`).
   * Proibido usar Data Annotations de ORM (`[Table]`, `[Key]`) no Domínio. Mapeamentos residem na Infraestrutura via `IEntityTypeConfiguration<T>`.
2. **Proibição de MediatR e AutoMapper:**
   * Qualquer inclusão desses pacotes quebrará a auditoria da solução. Utilize CQRS Lite nativo e Extension Methods de mapeamento.
3. **Endpoints Carter e Envelope `JsonResponse`:**
   * Endpoints HTTP devem herdar de `CarterModule` e expor metadados OpenAPI (`.WithOpenApi()`).
   * 100% das respostas HTTP devem ser envelopadas em `JsonResponse<TData, TError>`. Proibido retornar tipos crus ou `Results.Ok(...)` sem o envelope.
4. **Segurança em Consultas Dapper:**
   * É estritamente proibida concatenação ou interpolação de strings (`$"SELECT ... WHERE id = {id}"`). Todas as consultas devem utilizar parâmetros nomeados tipados (`new { Id = id }`).
5. **Resiliência e Mensageria no Kafka:**
   * **Producers:** Devem operar com `Acks = Acks.All` e `EnableIdempotence = true`.
   * **Consumers (`BackgroundService`):**
     * `EnableAutoCommit = false` e `EnableAutoOffsetStore = false`. O commit do offset DEVE ser efetuado manualmente após o sucesso da transação do negócio.
     * **Inbox Pattern:** Checagem de duplicação por chave única antes de aplicar efeitos colaterais.
     * **Poison Pills & DLQ:** Erros de deserialização/incompatibilidade devem ser roteados para o tópico `.DLQ` com headers diagnósticos (`x-original-topic`, `x-exception-message`, etc.).
     * **Polly:** Aplicar Retry com Exponential Backoff e Circuit Breaker para falhas transientes de infraestrutura.
     * Honrar o `CancellationToken` no loop de consumo para suportar *Graceful Shutdown*.
6. **Qualidade de Testes (Padrão AAA):**
   * Todo Handler gerado deve possuir testes unitários correspondentes em `tests/UnitTests`.
   * Estrutura visual obrigatória com comentários `// Arrange`, `// Act`, `// Assert`.
   * Uso exclusivo de asserções expressivas via `FluentAssertions` (`.Should().Be(...)`).
7. **Formatação Determinística:**
   * O código gerado deve passar com sucesso no comando `dotnet format --verify-no-changes`.
8. **Conformidade Estrita com as Especificações de Banco de Dados:**
   * Toda modelagem de entidade, migration EF Core, query Dapper ou índice DEVE seguir rigorosamente os esquemas, tipos, constraints e tabelas de resiliência (Outbox/Inbox) definidos em `.agents/specification/db/`:
     - `catalog-events.md` (Domínio de Catálogo de Eventos)
     - `reservation-db.md` (Domínio de Reserva e Distributed Lock no Redis)
     - `payment-db.md` (Domínio de Pagamento e Ledger Financeiro)
     - `ticketing-db.md` (Domínio de Ticketing e Alocação Atômica de Assentos)
     - `search-db.md` (Domínio de Busca e ElasticSearch)
   * Nenhuma constraint de unicidade (`UNIQUE`), tipo de dado, chave composta ou política de retenção de Outbox/Inbox pode ser omitida ou alterada sem atualização prévia da respectiva especificação.
