# Diretrizes do Projeto - Sistema de Reserva de Ingressos (TicketReserver.IO)

Este documento define as diretrizes de arquitetura, padrões de design e stack tecnológica para o desenvolvimento do sistema de reservas de ingressos de alta concorrência (estilo Ticketmaster).

O sistema é orientado a eventos (Event-Driven Architecture), focado em resiliência, consistência eventual e proteção extrema contra alta concorrência (Bottlenecks de Inventário).

---

## 1. Visão Geral da Arquitetura

O ecossistema é composto por microserviços independentes que se comunicam de forma assíncrona. Em nível de código, padronizamos a **Arquitetura Hexagonal** combinada com **CQRS Lite**.

### 1.1. Arquitetura Hexagonal (Ports and Adapters)
O objetivo é isolar completamente a lógica de negócio (Domínio) das preocupações de infraestrutura (Bancos de dados, Mensageria, APIs externas).
*   **Core (Domínio + Aplicação):** Não possui dependências externas (sem pacotes do Entity Framework, Kafka ou ASP.NET Core). Depende apenas do BCL (Base Class Library) do .NET.
*   **Ports (Interfaces):** Definidas no Core. Representam o que a aplicação precisa (ex: `IReservationRepository`, `IMessagePublisher`).
*   **Adapters (Infraestrutura):** Implementam os Ports. É aqui que vivem o Entity Framework Core, Redis, MassTransit/Kafka e integrações HTTP.
*   **Primary Adapters (Apresentação):** Controladores da Web API, Workers ou gRPC endpoints que acionam a camada de Aplicação.

### 1.2. CQRS Lite (Command Query Responsibility Segregation)
Utilizamos o padrão CQRS Lite mediado pela biblioteca **MediatR**.
*   **Commands:** Operações que alteram o estado do sistema (ex: `CreateReservationCommand`, `ProcessPaymentCommand`).
*   **Queries:** Operações de leitura pura, sem efeitos colaterais (ex: `GetEventDetailsQuery`).
*   **Bancos de Dados:** Enquanto o microserviço de Busca utiliza CQRS físico (Postgres -> Debezium -> ElasticSearch), os demais serviços utilizam CQRS lógico na mesma base de dados.

---

## 2. Stack Tecnológica (.NET Ecosystem)

*   **Linguagem & Framework:** C# 12 / .NET 8 (ou superior).
*   **API & Comunicação:** ASP.NET Core Web API (Minimal APIs).
*   **Mediação (CQRS):** MediatR.
*   **Mensageria & Outbox:** MassTransit (Abstração sobre o Apache Kafka).
*   **Persistência Relacional:** Entity Framework Core + PostgreSQL.
*   **Cache & Lock Distribuído:** StackExchange.Redis.
*   **Busca:** NEST (ElasticSearch client para .NET).
*   **Telemetria:** OpenTelemetry (OTLP) integrado com Grafana/Jaeger.

---

## 3. Padrões de Sistema e Resiliência (System Design)

A arquitetura garante consistência distribuída e proteção contra alta concorrência através dos seguintes padrões:

*   **Seat Hold (Distributed Lock):** Utiliza o Redis (`SET NX EX`) para garantir reserva atômica na memória RAM antes de qualquer gravação no Postgres. Protege o banco de dados contra *Row-Level Locking* e *Deadlocks* durante picos de vendas.
*   **Transactional Outbox:** Obrigatório em todas as transações que alteram estado e publicam eventos. Utilizamos a implementação nativa de Outbox do **MassTransit** em conjunto com o EF Core.
*   **Saga Coreografada:** O fluxo de negócio (Hold -> Checkout -> Pagamento -> Ticketing) é governado por eventos.
*   **Compensação e Estorno (Rollback):**
    *   *Fast Fail:* Se o pagamento for recusado, a reserva é cancelada e o Lock no Redis é destruído instantaneamente.
    *   *Auto-Refund:* Se houver falha de concorrência na emissão final do ingresso (após aprovação do pagamento), um worker dedicado aciona o gateway de pagamento para estorno automático.
*   **Delayed Messages (Timeout):** Utilizado para cancelar reservas (`AWAITING_PAYMENT`) caso o usuário abandone o carrinho após os 10 minutos de Lock.

---

## 4. Estrutura de Solução e Projetos

Cada microserviço deve seguir a seguinte estrutura de solução no .NET:

```text
TicketSystem.Reservation/
├── src/
│   ├── TicketSystem.Reservation.Domain/        # Entidades, Value Objects, Enums, Exceptions
│   ├── TicketSystem.Reservation.Application/   # CQRS (Commands/Queries), Handlers, Ports (Interfaces)
│   ├── TicketSystem.Reservation.Infrastructure/# EF Core, Repositories, Redis Lock, MassTransit
│   └── TicketSystem.Reservation.Api/           # Program.cs, Minimal APIs (Endpoints), Middlewares
├── tests/
│   ├── TicketSystem.Reservation.UnitTests/
│   └── TicketSystem.Reservation.IntegrationTests/

```

---

## 5. Diretrizes de Desenvolvimento (Best Practices)

1. **Regra de Ouro do Domínio:** A camada `Domain` é sagrada. Se houver um `using Microsoft.EntityFrameworkCore;` ou `using MassTransit;` no projeto de Domínio, o PR será rejeitado.
2. **Uso do MediatR no Controller:** Os endpoints da API (`Program.cs` ou Controllers) não devem conter lógica de negócio. Eles devem apenas mapear o Request HTTP para um `ICommand` ou `IQuery` e despachar para o `IMediator`.
3. **Transações e Outbox:**
* Nenhum evento de domínio deve ser publicado diretamente no broker dentro da lógica de negócio.
* Utilize o Outbox pattern do MassTransit. O salvamento no banco (`DbContext.SaveChangesAsync`) garantirá a persistência atômica da entidade e da mensagem no banco relacional.
4. **Resiliência a Falhas:**
* Comandos que dependem de integrações externas (ex: Pagamento) devem ter políticas de *Retry* configuradas (Polly / MassTransit Retry Filters) com *Exponential Backoff*.
* Mensagens reincidentes em falhas devem ser roteadas para uma *Dead Letter Queue (DLQ)*.
5. **Idempotência:** Todo Handler que consome mensagens do Kafka deve ser desenhado de forma idempotente. A mesma mensagem entregue duas vezes não deve gerar dois pagamentos ou dois ingressos. (Utilize validação por `EventId` ou chaves únicas no banco).
6. **Telemetria:** Todos os serviços devem registrar Traces e Metrics via OpenTelemetry no `Program.cs`. Propague o `traceparent` no cabeçalho HTTP e nos headers do Kafka para rastreabilidade ponta a ponta.