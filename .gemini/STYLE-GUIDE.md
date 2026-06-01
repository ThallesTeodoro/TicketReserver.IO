# C# & .NET Style Guide (style-guide.md)

Este guia de estilo é a lei absoluta de codificação para o ecossistema **TicketReserver.IO**. A IA deve aplicar estas regras rigorosamente em todas as gerações de código.

## 1. Padrões Arquiteturais e de Design
*   **Arquitetura Hexagonal (Ports & Adapters):** O núcleo da aplicação (Domain e Application) deve ser totalmente agnóstico de tecnologias externas. O mundo externo só se comunica com a aplicação através de *Ports* (Interfaces) e *Adapters* (Implementações de Infraestrutura).
*   **CQRS Lite:** Separe claramente as intenções de escrita (Commands) das intenções de leitura (Queries). Não há necessidade de infraestrutura separada de banco para isso; apenas separe as classes e manipuladores (`Handlers`/`UseCases`).
*   **Padrão Repository com Dapper:** Toda persistência e consulta ao banco de dados PostgreSQL deve utilizar o **Dapper** (Micro-ORM) implementando a interface do repositório correspondente.
*   **Abordagem de APIs:** Priorize a utilização de **Minimal APIs** em vez de controllers tradicionais.
*   **Rede e Proteção:** A única API exposta publicamente via Kubernetes Ingress/Gateway será a `TR.API.Reservation`. Todos os outros workers e componentes de infraestrutura estarão protegidos dentro da rede interna do cluster.

## 2. Nomenclatura e Namespaces (Padrão TR)
Todos os projetos, soluções e namespaces devem seguir a convenção de nomenclatura corporativa baseada no prefixo `TR` (Ticket Reservation):

*   **APIs Expostas:** `TR.API.[NomeComponente]` (Ex: `TR.API.Reservation`)
*   **Workers e Consumidores Independentes:** `TR.HW.[NomeComponente]` (HW = Hosted Worker / Componente Isolado) (Ex: `TR.HW.Payment`, `TR.HW.Ticketing`, `TR.HW.OutboxRelay`)

A estrutura interna de namespaces dentro de um componente Hexagonal deve respeitar:
*   `TR.[Tipo].[Componente].Domain`
*   `TR.[Tipo].[Componente].Application`
*   `TR.[Tipo].[Componente].Infrastructure`

## 3. Práticas de Código e Mapeamento
*   **Modelos Não Anêmicos:** Entidades do Domínio devem possuir estado e comportamento auto-validados. O construtor ou métodos de fábrica (Factory Methods) devem garantir que a entidade nunca seja criada em estado inválido. Propriedades devem ter setters privados (`private set`).
*   **Proibição do AutoMapper:** É expressamente **PROIBIDO** o uso de AutoMapper ou ferramentas de reflexão semelhantes. Todo e qualquer mapeamento entre Entidades, DTOs e Commands deve ser feito de forma explícita através de **Métodos de Extensão** C# estáticos (Ex: `public static ReservationOutput ToOutput(this Reservation entity)`).
*   **Early-Return:** Código limpo evita aninhamento de `if/else`. Aplique a validação de guarda (*Guard Clauses*) e retorne o fluxo o mais cedo possível.
*   **Integração Externa:**
    *   Comunicação HTTP com terceiros: Utilizar a biblioteca **Refit** para geração de clientes HTTP baseados em interfaces.
    *   Comunicação Mensageria: Utilizar a biblioteca oficial da **Confluent** (`Confluent.Kafka`) para interagir com o broker.

## 4. Tratamento de Erros, Exceções e Saúde
*   **Exceções Declarativas:** Não use exceções genéricas da plataforma. Crie exceções de negócio tipadas herdando de uma classe base do domínio. Exemplos obrigatórios: `NotFoundException`, `BusinessException`, `ValidationException`.
*   **ExceptionHandlers:** Utilize o middleware nativo do .NET (`IExceptionHandler`) centralizado para capturar as exceções declarativas e convertê-las em respostas HTTP padronizadas (padrão RFC 7807 - Problem Details).
*   **Health Checks:** Todos os projetos `TR.API` e `TR.HW` devem expor endpoints de saúde de aplicação (`/healthz` para liveness/readiness), monitorando conexões com Postgres, Redis e Kafka.

## 5. Padrões de Teste de Software
*   **Stack:** Utilizar **Moq** para isolamento de dependências de infraestrutura e **FluentAssertions** para asserções legíveis.
*   **Escopo Obrigatório:** Cada UseCase/Command gerado deve possuir testes unitários cobrindo:
    1. Cenário de sucesso total.
    2. Exceções de negócio mapeadas.
    3. Testes de borda (valores limites, estouro de dados).
    4. Validação de nulidade, strings vazias ou coleções vazias.

## 6. Fluxo de Desenvolvimento Estrito de uma Feature

Sempre que a IA receber a tarefa de criar uma nova funcionalidade, ela deve guiar o desenvolvimento sequencialmente através destes 7 passos, sem pular nenhum:

1.  **Domain - Entidade:** Criar a Entidade de Domínio rica com validações no construtor e métodos de negócio.
2.  **Domain - Porta de Entrada (Repository Interface):** Definir a interface do repositório no domínio.
3.  **Application - Contratos de Entrada/Saída:** Criar as estruturas de `Command`/`Query` e o objeto de `Output`.
4.  **Application - Mappers:** Escrever as classes estáticas de extensão para mapear de/para a Entidade.
5.  **Infrastructure - Adaptador (Repository Implementation):** Implementar o repositório usando Dapper, envolvendo as chamadas SQL em políticas de resiliência do **Polly** (Retry transient faults).
6.  **Infrastructure - Comunicação Externa (Se aplicável):** Definir as interfaces de API via Refit ou Producers com a biblioteca da Confluent.Kafka (Sempre valide os tópicos no arquivo `specification/02-contracts.md`).
7.  **Delivery - Minimal API / UseCase Application:** Criar a rota na Minimal API injetando o manipulador e disparando a execução.
8.  **Test - Validação de Unidade:** Escrever a suite de testes cobrindo sucesso, falhas declarativas e regras de borda usando Moq e FluentAssertions.