# Módulos do SMSMais

Esta pasta organiza a **documentação por módulo de negócio** do ecossistema SMSMais.
A ideia é tratar cada grande frente (transporte/TFD, laudos, agendamento, importação de
PEPs, etc.) de forma isolada — escopo, arquitetura, requisitos e cronograma próprios —
sem misturar com a documentação canônica transversal de [`docs/`](../).

## Por que separar por módulo?

O monorepo já é multi-projeto (ver [`CLAUDE.md`](../../CLAUDE.md)) e os módulos evoluem
em ritmos diferentes, muitas vezes com clientes/áreas distintas. Uma pasta por módulo
permite:

- **Escopo entregável por módulo** — um documento que o cliente lê e aprova sem
  precisar do contexto inteiro do sistema.
- **Cronograma independente** — cada módulo tem seu ciclo de ajuste → desenvolvimento →
  teste → piloto → implantação.
- **Rastreabilidade** — requisitos numerados, critérios de aceite e referências cruzadas
  para os ADRs canônicos em [`docs/adr/`](../adr/).

## Relação com `docs/`

| Onde | O quê |
|------|-------|
| [`docs/`](../) | Documentação **transversal e canônica**: visão, arquitetura geral, domínio, banco, convenções, roadmap e os **ADRs** (decisões arquiteturais imutáveis). |
| `docs/modulos/<modulo>/` | Documentação **de produto/projeto por módulo**: escopo, requisitos, arquitetura do módulo, cronograma e apresentação. **Não substitui** os ADRs — referencia-os. |

> Regra: decisões arquiteturais que afetam o sistema todo continuam virando **ADR** em
> `docs/adr/`. A pasta do módulo descreve **o projeto** (escopo, prazo, entregáveis) e
> pode propor um ADR novo (ex.: o módulo TFD propõe o **ADR-0017**).

## Convenção de estrutura de um módulo

```
docs/modulos/<modulo>/
  README.md          # índice do módulo: o que é, links, status
  escopo.md          # escopo entregável (cliente): objetivos, in/out, premissas, riscos, custos
  requisitos.md      # requisitos funcionais (RF) + não-funcionais (RNF) + critérios de aceite
  arquitetura.md     # arquitetura técnica do módulo: modelo de dados, integrações, fluxos
  cronograma.md      # cronograma faseado (ajuste→dev→teste→piloto→implantação) + esforço
  apresentacao.md    # apresentação executiva (slide-style) para o cliente/gestores
```

## Módulos documentados

| Módulo | Pasta | Status |
|--------|-------|--------|
| **TFD — Tratamento Fora do Domicílio** (transporte sanitário de pacientes) | [`tfd/`](./tfd/) | 📄 Escopo + cronograma em aprovação |

> Próximos candidatos a ganhar pasta própria: Laudos/Assinatura digital, Agendamento +
> SISREG, Importação de PEPs (Salux→FHIR), Módulo IA.
