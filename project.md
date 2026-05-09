# TicketReserver.IO - Capstone Project

## 1. Visão Geral
O **TicketReserver.IO** é um sistema distribuído de alta concorrência para reserva e venda de ingressos. O objetivo principal deste projeto é consolidar hard skills de nível sênior em arquitetura de microsserviços, focando em resiliência, consistência eventual e observabilidade profunda.

Este projeto faz parte de um **Plano de Desenvolvimento Individual (PDI)** focado na stack .NET e tecnologias de infraestrutura modernas.

## 2. Objetivos de Negócio
*   Garantir a reserva de assentos por tempo limitado (10 minutos).
*   Processar pagamentos de forma assíncrona e resiliente.
*   Impedir cobranças duplicadas através de mecanismos de idempotência.
*   Garantir que, em caso de falha no pagamento, o assento seja liberado automaticamente (SAGA Compensação).

## 3. Core Hard Skills (PDI Focus)
*   **Sistemas Distribuídos:** Implementação de SAGA Coreografada.
*   **Mensageria:** Aprofundamento em Apache Kafka (Consumer Groups, Offsets, Headers).
*   **Resiliência:** Outbox Pattern (componente segregado) e Dead Letter Queues (DLQ).
*   **Cloud Native:** Orquestração com Kubernetes (K8s), Autoscaling (HPA) e Probes.
*   **Observabilidade:** OpenTelemetry (OTel), Tracing Distribuído e Logging Estruturado com Grafana Loki.

## 4. Stack Tecnológica
*   **Linguagem:** C# .NET 9 (ou .NET 8).
*   **Broker:** Apache Kafka.
*   **Bancos de Dados:** PostgreSQL (Persistência) e Redis (Idempotência de entrada).
*   **Orquestração:** Kubernetes (K8s) via Docker Desktop.
*   **Observabilidade:** OpenTelemetry Collector, Grafana Loki (Logs), Grafana Tempo (Traces).
*   **DevOps:** Docker, Docker-compose e manifestos K8s.

## 5. Arquitetura de Alto Nível
O sistema é composto por três domínios principais:
1.  **Service-Reservation:** API de entrada, controle de assentos e Outbox Table.
2.  **Outbox-Relay:** Componentes segregados (Workers) que extraem dados do DB e publicam no Kafka (Um para cada domínio).
3.  **Service-Payment:** Worker de processamento financeiro com controle de idempotência transacional.
4.  **Service-Ticketing:** Worker de emissão de ingressos após confirmação de pagamento.

## 6. Padrões de Projeto & Estratégias
*   **Idempotência:** Híbrida (Redis na entrada da API / Unique Constraint no DB de Pagamento).
*   **Consistência:** Eventual via SAGA Coreografada.
*   **Comunicação:** Assíncrona via Kafka (Event-Driven).
*   **Tratamento de Erros:** Retry Policies (Polly) e redirecionamento para DLQ no Kafka.
*   **Rastreio:** Injeção de Contexto (TraceId) via Kafka Headers para propagação de traces.

## 7. Estrutura de Pastas do Projeto
*   `/specification`: Detalhamento técnico de cada componente e fluxo.
    *   `business-rules.md`
    *   `arch-decision-records.md` (ADRs)
    *   `kafka-topics.md`
    *   `db-schemas.md`
    *   `observability-spec.md`
*   `/planning`: Roteiro de desenvolvimento e Sprints.
    *   `backlog.md`
    *   `sprint-01.md` (Infra e Base)
    *   `sprint-02.md` (SAGA e Resiliência)
*   `/codebase`: Onde ficarão os códigos fontes
*   `/documentation`: Local futuro da documentação do projeto

## 8. Definição de Pronto (DoP)
O projeto será considerado concluído quando:
- [ ] Um fluxo de ponta a ponta (Reserva -> Pagamento -> Emissão) for rastreado no Grafana via um único Trace ID.
- [ ] O componente Outbox Relay garantir a entrega no Kafka mesmo após quedas temporárias do broker.
- [ ] O mecanismo de idempotência impedir duplicidade de cobrança em 100% dos retries simulados.
- [ ] O cluster K8s escalar automaticamente os pods da API sob carga de estresse.

## 9. Pergunte antes de tomar decisões de forma autônoma, especialmente se encontrar situações como essas:
- Informações conflitantes entre as definições
- Falta de informação suficiente para realizar a implementação
- Sugestões de melhorias de performance, especificação ou implementação

## 10. Dinâmica de construção do software
- Neste projeto, trabalharemos em 3 etapas:
    - Planejamento: realizaremos todas as atividades relacionadas às especificações e planejamento do projeto. Nesta fase, apenas o detalhamento máximo é desejável, não iremos produzir código até que todas as especificações sejam concluídas e o ok final seja dado. A cada etapa ou criação de arquivo de especificação novo, deve-se solicitar a revisão antes de continuar para o próximo.
    - Desenvolvimento: nesta etapa, utilizando todas as informações geradas na etapa de planejamento, iniciaremos de fato o desenvolvimento do código de acordo com as etapas definidas no planning. Todas as instruções e validações devem ser seguidas a risca. Nenhuma implementação deve exceder ou faltar o escopo do que está definido em cada sprint da pasta "/planning".
        - Para cada arquivo de sprint na pasta "/planning" teremos mais 2 arquivos adicionais:
            - `sprint{n}_tasks.md`: Onde serão detalhadas todas as atividades necessárias para realização da sprint.
            - `sprint{n}_logs.md`: Onde serão documentadas todas as ações realizadas pelo agente naquela sprint.
    - No final do desenvolvimento, realizaremos o code review para validar o que fizemos. Se encontrado algum problema, documentaremos mas não alteraremos nada sem instrução explicita.

## 11. Expansão de Contexto
Os documentos na pasta "/specification", "/source" e os "-logs" das sprints anteriores servem de contexto para para definição das sprints, tasks e escrita do codebase de acordo com a sprint atual.
