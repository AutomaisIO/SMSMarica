# ADR-0002 — Modular Monolith + Clean Architecture por módulo

- **Status**: **Superseded por [ADR-0004](./0004-arquitetura-tres-projetos.md)** em 2026-04-22
- **Data original**: 2026-04-22
- **Decisores**: Bruno (product/eng)

> **Nota da reversão (2026-04-22):** após scaffold completo de 44 projetos, avaliamos que a divisão Modular Monolith + Clean Architecture por módulo era **desproporcional para o estágio atual**. A nova decisão (ADR-0004) reduz a arquitetura para **3 projetos** (Data + Core + Api), mantendo as mesmas entidades como POCOs simples. O conteúdo abaixo permanece como registro histórico do raciocínio original.

---

## Contexto

O backend `SMSMarica.server` tem pelo menos **9 subdomínios** mapeados (Pacientes, Tratamentos, Unidades, Veículos, Motoristas, Translado, Rastreamento, Avaliações, Identidade) e a expectativa de crescer — integrações com sistemas legados, novas áreas de operação, possivelmente domínios vizinhos ainda não mapeados. Time atual é pequeno; operação da infra é limitada.

Precisamos escolher como organizar o código para:

- Crescer sem virar um "lago de tabelas e services" sem fronteiras.
- Permitir executar várias features em paralelo (inclusive com Claude Code em múltiplas instâncias) sem que uma atrapalhe a outra.
- Preservar a opção de extrair um domínio para serviço separado no futuro (candidato natural: **Rastreamento**, quando o volume de GPS forçar).

## Decisão

Adotar **Modular Monolith** com **Clean Architecture (4 camadas) dentro de cada módulo**:

```
src/
├── Host/SMSMarica.Api                        (único executável)
├── BuildingBlocks/                            (código transversal)
│   ├── Domain
│   ├── Application
│   ├── Infrastructure
│   └── Api
└── Modules/<Modulo>/
    ├── .Domain
    ├── .Application
    ├── .Infrastructure
    └── .Api
```

**Regras duras** aplicadas pelo compilador (via ausência de `ProjectReference`):

1. `Module.Domain` depende **apenas** de `BuildingBlocks.Domain`.
2. `Module.Application` depende de `Module.Domain` + `BuildingBlocks.Application`.
3. `Module.Infrastructure` depende de `Module.Application` + `BuildingBlocks.Infrastructure`.
4. `Module.Api` depende de `Module.Application` + `BuildingBlocks.Api`.
5. **Módulo A NUNCA referencia Módulo B**. Integração é via:
   - evento via Outbox (preferido);
   - contrato público compartilhado como parte de Application;
   - chamada HTTP interna (fallback).

Composição dos módulos no Host via interface `IApiModule` descoberta por reflexão.

## Alternativas consideradas

### A. Clean Architecture "flat" (4 projetos: Api / Application / Domain / Infrastructure)

**Prós:** menos cerimônia, padrão mainstream, muita literatura.
**Contras:** com 9+ subdomínios as pastas viram um paiol; fronteiras entre subdomínios se dissolvem dentro do mesmo projeto `Domain`; refactor fica caro quando um subdomínio cresce. Dificulta paralelismo entre instâncias porque todas mexem nos mesmos projetos.
**Rejeitada.**

### B. Microserviços desde o início

**Prós:** isolamento máximo; escala independente.
**Contras:** overhead operacional pesado (um cluster que já é compartilhado, time pequeno, pipelines por serviço, observabilidade distribuída). Nenhuma das pressões que justificam microserviços está presente hoje — não há escala por domínio diferente, não há times independentes, não há lançamentos independentes.
**Rejeitada** — pode ser reconsiderada para domínios específicos (Rastreamento) quando o volume justificar.

### C. Vertical Slice Architecture (sem camadas, agrupando tudo por feature)

**Prós:** excelente para times que não querem "ceremony of layers"; cada request é um arquivo.
**Contras:** dilui a responsabilidade do Domain; regras de negócio tendem a vazar para handlers; dificulta testes de domínio puros. Em projetos com regras fortes (periodicidade, alocação), queremos Domain robusto.
**Rejeitada como padrão**; pode ser adotado pontualmente dentro de um módulo para features muito simples.

### D. Modular Monolith sem Clean Architecture por módulo (cada módulo é 1 projeto)

**Prós:** menos projetos.
**Contras:** perde a separação Domain/Application/Infra dentro do módulo — Infra passa a conviver com o agregado, que é exatamente o que queremos evitar em domínios ricos.
**Rejeitada.**

## Consequências

### Positivas

- Compilador enforca fronteiras.
- Módulos podem ser implementados em paralelo por instâncias separadas sem conflito de merge na camada de domínio.
- Extração futura de um módulo para serviço: as 4 camadas já estão prontas, basta adicionar hospedagem própria e trocar chamadas in-process por HTTP/mensagens.
- Testes de domínio ficam isolados e rápidos.

### Negativas / Custo

- **Número de projetos grande** (~44 na S1): ~4s a mais no primeiro build; negligenciável em CI.
- **Template inicial pesado**: o primeiro módulo (Pacientes) serve de referência e é duplicado pelos outros. Assumido como custo único.
- **Duplicação aparente** em DbContexts (cada módulo tem o seu): é intencional — cada módulo dono da sua persistência.

### Condições para revisitar

- Se a manutenção dos BuildingBlocks virar gargalo global.
- Se um módulo crescer ao ponto de precisar de escala independente → extrair para microserviço (não reverter para flat).
- Se aparecer um domínio genuinamente "orthogonal" que não caiba como módulo → criar novo trilho de building blocks.
