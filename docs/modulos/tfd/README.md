# Módulo TFD — Tratamento Fora do Domicílio

> **TFD (Tratamento Fora do Domicílio)** é a política do SUS (Portaria SAS/MS nº 55/1999)
> que garante o **transporte** — e, quando indicado, ajuda de custo e acompanhante — de
> pacientes que precisam de atendimento de saúde **em outro município**, quando o
> procedimento não está disponível na rede local. Em Maricá, o módulo TFD do SMSMarica
> organiza **quem vai, em qual carro, por qual rota, com qual motorista e quando**,
> levando pacientes (e acompanhantes) da cidade até hospitais e unidades de referência
> em outras cidades, e trazendo-os de volta.

Este módulo cobre o ciclo completo do transporte: **cadastro → planejamento →
geração inteligente de rotas → execução no carro → comunicação com o paciente →
rastreamento → conclusão**.

---

## Documentos deste módulo

| Documento | Para quem | Conteúdo |
|-----------|-----------|----------|
| [`escopo.md`](./escopo.md) | **Cliente** (SMS Maricá + OS Avante) | Objetivos, escopo funcional (10 frentes), o que já existe vs. o que é novo, fora de escopo, premissas, riscos, custos operacionais. |
| [`cronograma.md`](./cronograma.md) | **Cliente + equipe** | Cronograma de **20 dias corridos** (24/06→13/07/2026): ajuste → desenvolvimento → teste → piloto → implantação, marcos, caminho crítico e Fase 2. |
| [`proposta-tfd.html`](./proposta-tfd.html) | **Cliente (PDF)** | Documento visual com identidade Maricá + **gráfico de Gantt** — pronto para exportar em PDF. |
| [`apresentacao.md`](./apresentacao.md) | **Secretaria + OS** | Apresentação executiva (formato slides) — valor, fases e entregáveis. |
| [`requisitos.md`](./requisitos.md) | **Equipe** | Requisitos funcionais (RF) e não-funcionais (RNF) numerados + critérios de aceite + matriz de rastreabilidade. |
| [`arquitetura.md`](./arquitetura.md) | **Equipe técnica** | Modelo de dados, motor de otimização (Google Maps + Claude), WhatsApp (Meta), apps, integrações e ADR-0017 proposto. |
| [`faturamento.md`](./faturamento.md) | **Equipe + gestão** | Faturamento SUS/BPA: regra de 1 unidade/50 km, modelo, fluxo e exportação. |

---

## Resumo de uma página

**O que o módulo entrega:**

1. **Destino por tratamento** — cada tratamento aponta para a unidade/hospital de destino (em Maricá ou fora), com endereço geolocalizado.
2. **Acompanhante** — saber, antes do dia, se o paciente vai **com ou sem acompanhante**, confirmado **pelo próprio paciente via WhatsApp**, reservando o assento certo.
3. **Geração inteligente de translado** — com **Google Maps + Claude**, o sistema **distribui os pacientes nos carros** e calcula **duas rotas otimizadas**: a de **coleta** (pegar todo mundo em Maricá) e a de **entrega** (levar cada um ao seu destino), e a volta.
4. **App do motorista (tablet)** — o motorista recebe a **lista de pacientes a coletar** em ordem com a **rota pronta**, navega, e à medida que pacientes vão sendo **liberados**, recebe novos embarques; pode também **"puxar" pacientes que estão aguardando**.
5. **"Puxar" pacientes fora de Maricá** — na volta, o motorista vê os pacientes **já fora de Maricá** com a **distância** de cada um e pode **puxar o paciente (+ acompanhante)** para a sua rota.
6. **WhatsApp do paciente** — confirmação de acompanhante, aviso de coleta, horário/ETA.
7. **App do cidadão (paciente)** — acompanhar o translado, confirmar acompanhante, ver o carro chegando em tempo real e avaliar o serviço.
8. **Painel do gestor (web)** — gerar translado com um clique, ver as rotas no mapa, acompanhar a frota ao vivo.

**Base que já existe** (reduz risco e prazo): rotas diárias, alocação de pacientes em
assentos, mapa de assentos por veículo (com tipo "acompanhante"), tratamentos com
periodicidade e sessões, cadastro de motoristas/veículos/unidades, ingestão de GPS e
geofences, **Claude já integrado** ao backend, e o app do motorista já scaffoldado.

**Frente | Equipe | Prazo:** equipes dedicadas (desenvolvimento, implantação e suporte) ·
**20 dias corridos** (24/06→13/07/2026) até o piloto operacional · WhatsApp oficial (Meta
Cloud API, com conta da Secretaria).

---

## Status

- **2026-06-18** — Escopo, cronograma e apresentação elaborados para aprovação da SMS
  Maricá e da OS Avante (A/C Sra. Clarisse). Aguardando **kickoff** (define o D1 do
  cronograma) e início da **verificação de negócio na Meta** (WhatsApp).
