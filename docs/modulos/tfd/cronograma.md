# Cronograma — Módulo TFD (20 dias corridos)

> Plano de **20 dias corridos** para entregar o **piloto operacional** do módulo TFD,
> cobrindo as cinco fases: **Ajuste → Desenvolvimento → Teste → Piloto → Implantação**.
> **Início: quarta-feira, 24/06/2026 · Piloto operando: segunda-feira, 13/07/2026.**
> Versão visual (gráfico de Gantt) para o cliente em [`proposta-tfd.html`](./proposta-tfd.html).
> Escopo e frentes (FT0–FT9) em [`escopo.md`](./escopo.md); desenho técnico em
> [`arquitetura.md`](./arquitetura.md).

---

## 1. Premissas do cronograma

- **Equipe:** equipes dedicadas — **desenvolvimento**, **implantação** e (pós-go-live)
  **suporte** —, com **apoio intensivo de IA** na implementação.
- **Reuso:** ~50% da fundação já existe (cadastros, rota diária, assentos, GPS, Claude,
  apps scaffoldados) — isto é o que torna 20 dias corridos viável.
- **Meta = piloto operacional** com **1–2 veículos** e pacientes reais; a operação em
  escala plena (toda a frota) ocorre na **expansão pós-piloto (Fase 2)**.
- **Dias corridos:** o cronograma inclui fins de semana (D4–D5, D11–D12, D18–D19).
- **Kickoff = D1 = 24/06/2026 (quarta-feira).** Datas-âncora:

  | | | | |
  |---|---|---|---|
  | D1 = **24/06** (qua) | D6 = 29/06 (seg) | D11 = 04/07 (sáb) | D16 = 09/07 (qui) |
  | D2 = 25/06 (qui) | D7 = 30/06 (ter) | D12 = 05/07 (dom) | D17 = 10/07 (sex) |
  | D3 = 26/06 (sex) | D8 = 01/07 (qua) | D13 = 06/07 (seg) | D18 = 11/07 (sáb) |
  | D4 = 27/06 (sáb) | D9 = 02/07 (qui) | D14 = 07/07 (ter) | D19 = 12/07 (dom) |
  | D5 = 28/06 (dom) | D10 = 03/07 (sex) | D15 = 08/07 (qua) | D20 = **13/07** (seg) |

- **Trilhas paralelas externas (críticas):** começam **no D1** e correm em paralelo — são os
  principais riscos de prazo (ver §5 e [`escopo.md` FT0/FT6](./escopo.md)):
  1. **Conta Meta da Secretaria + verificação de negócio** (WhatsApp oficial);
  2. **Acesso às APIs do SUS (DATASUS)** — ofício do Secretário nomeando o responsável
     técnico (documentos já enviados à Avante).

---

## 2. Visão geral — fases × período

| Fase | Dias | Datas | Foco |
|------|------|-------|------|
| **1 — Fundação & ajuste** | D1–D5 | 24–28/06 | Contas/credenciais, **conta Meta + documentos**, ADR-0017, destino+localização, acompanhante, base WhatsApp |
| **2 — Geração das viagens** (núcleo) | D6–D11 | 29/06–04/07 | Motor de translado: distribuição + rotas otimizadas (Maps + Claude) + painel |
| **3 — Apps & tempo real** | D12–D17 | 05–10/07 | App motorista (tablet), rastreamento/ETA/"puxar", app cidadão, avisos WhatsApp |
| **4 — Teste · Piloto · Implantação** | D18–D20 | 11–13/07 | Testes, treinamento, cutover WhatsApp oficial, piloto assistido, go/no-go |

---

## 3. Gantt (visual)

O gráfico de barras está em **[`proposta-tfd.html`](./proposta-tfd.html)** (seção 9 — pronto
para exportar em PDF). Resumo textual:

```
Atividade \ Período         24/06 ───────── 04/07 ───────── 13/07
Fundação & contas           ██
Verificação WhatsApp (Meta) ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓ (externo · paralelo)
Destino & mapa               ███
Acompanhante & WhatsApp        ██
Geração das viagens (núcleo)     ██████
Painel do gestor                    ███████
Tempo real & "puxar"                  █████
App do motorista (tablet)             ██████
App do cidadão                           ████
Testes & ajustes                            ███
Treinamento & piloto                          ███
Marcos:                     M1·28/06   M2·04/07   M3·10/07   M4·13/07
```

---

## 4. Detalhamento dia a dia

### Fase 1 — Fundação & ajuste (D1–D5 · 24–28/06)
| Dia | Data | Atividades | Entregável |
|-----|------|-----------|-----------|
| **D1** | 24/06 qua | Kickoff; **ADR-0017**; abrir **Google Cloud** + chaves Maps; **criar a conta Meta Business da Secretaria e iniciar a verificação de negócio**; **encaminhar o ofício de nomeação do responsável técnico para acesso às APIs do SUS (DATASUS)**; **submeter templates** WhatsApp; `HttpClient` + segredos cifrados. | Credenciais + ADR + verificação Meta + acesso DATASUS encaminhados |
| **D2** | 25/06 qui | **FT1** backend: localização no mapa (Google Geocoding) + cache `tfd_geocodigo` + migration; batch das unidades/pacientes. | Endereços com lat/long |
| **D3** | 26/06 sex | **FT1** front: destino na tela de tratamento; geocodificar ao salvar unidade; fila de revisão + **pin manual**. | Destino geolocalizado |
| **D4** | 27/06 sáb | **FT2** backend: acompanhante na sessão + migration; reserva do assento de acompanhante. | Acompanhante modelado |
| **D5** | 28/06 dom | **FT6** base: cliente WhatsApp (Meta Cloud API) + **webhook** idempotente; fluxo de **confirmação de acompanhante** (número de teste). | Confirmação por WhatsApp |
| | | **🏁 Marco M1 (28/06)** | Destino + acompanhante + WhatsApp de confirmação |

### Fase 2 — Geração das viagens / núcleo (D6–D11 · 29/06–04/07) ⭐
| Dia | Data | Atividades | Entregável |
|-----|------|-----------|-----------|
| **D6** | 29/06 seg | **FT3**: **Distance Matrix** + cache; matriz origem/destino do dia. | Matriz de distâncias |
| **D7** | 30/06 ter | **FT3**: **distribuição nos carros** + **Claude** (saída estruturada) com justificativa. | Distribuição automática |
| **D8** | 01/07 qua | **FT3**: **rota de coleta** otimizada (otimização de waypoints). | Rota de coleta |
| **D9** | 02/07 qui | **FT3**: **rota de entrega + retorno**; persistência + endpoint `POST /translados/gerar`. | Geração ponta a ponta |
| **D10** | 03/07 sex | **FT8**: botão **"Gerar translado"** + **mapa de rotas** + edição manual. | Geração pelo painel |
| **D11** | 04/07 sáb | Integração/ajuste do motor; início do **mapa ao vivo** no painel. | Motor estabilizado |
| | | **🏁 Marco M2 (04/07)** | Translado gerado (distribuição + coleta + entrega), auditável |

### Fase 3 — Apps & tempo real (D12–D17 · 05–10/07)
| Dia | Data | Atividades | Entregável |
|-----|------|-----------|-----------|
| **D12** | 05/07 dom | **FT5** backend: job GPS→**chegadas** + **ETA**; **SignalR**. | Tempo real no backend |
| **D13** | 06/07 seg | **FT5**: `AguardandoRetorno` + **distância** + **"puxar"**; **frota ao vivo** + fila. | "Puxar" + mapa ao vivo |
| **D14** | 07/07 ter | **FT4** app motorista: **tablet** + rota real + navegação + **GPS em background**. | App executa a rota |
| **D15** | 08/07 qua | **FT4**: recebimento **dinâmico** + **"puxar"** no app + embarque/desembarque; offline. | App motorista completo |
| **D16** | 09/07 qui | **FT7** app cidadão: agenda + confirmar acompanhante + **ETA**. **FT6**: avisos de coleta. | App cidadão (Fase 1) |
| **D17** | 10/07 sex | Integração fim-a-fim + ajustes finos. | Fluxo completo em teste |
| | | **🏁 Marco M3 (10/07)** | Fluxo operacional completo em ambiente de teste |

### Fase 4 — Teste · Piloto · Implantação (D18–D20 · 11–13/07)
| Dia | Data | Atividades | Entregável |
|-----|------|-----------|-----------|
| **D18** | 11/07 sáb | Testes integrados + RBAC + hardening (orçamento, LGPD, fallback); carga dos dados do piloto; **cutover WhatsApp oficial** (se verificação Meta aprovada). | Sistema endurecido |
| **D19** | 12/07 dom | **Treinamento** (motoristas, reguladores, atendentes) + **ensaio assistido (dry-run)**. | Equipe treinada |
| **D20** | 13/07 seg | **Piloto assistido** com 1–2 veículos e pacientes reais + retrospectiva + **plano de expansão**. | Piloto aceito + plano |
| | | **🏁 Marco M4 — Go/No-Go (13/07)** | Piloto aceito → autorização para expandir |

---

## 5. Caminho crítico e dependências

1. **Conta Meta da Secretaria + verificação de negócio (WhatsApp oficial)** — externa, lead
   time de dias a semanas. **Inicia no D1.** Depende de a SMS Maricá fornecer os documentos
   (CNPJ, ato oficial, comprovante de endereço, site/e-mail `gov.br`, representante — ver
   [`escopo.md` FT0](./escopo.md) e seção 10 da proposta). Se não concluir até o **D18**, o
   piloto roda em **número de teste** e o cutover oficial entra logo após o D20.
2. **Acesso às APIs do SUS (DATASUS)** — externo (Ministério da Saúde). Depende do **ofício
   do Secretário nomeando o responsável técnico**; documentos já enviados à Avante. Habilita
   a integração com a regulação/SISREG (demanda de TFD em fase posterior).
3. **Qualidade dos endereços** (localização no mapa) — alimenta a FT3; mitigada por pin manual.
4. **FT3 (motor)** é pré-requisito de FT4/FT5/FT8 reais — por isso ocupa a Fase 2 inteira.
5. **Dados do piloto** (motoristas, veículos, pacientes reais) prontos até o **D18**
   (responsabilidade da OS).

---

## 6. Esforço indicativo por frente (dev-dias)

> Soma > 20 porque há sobreposição entre frentes e forte apoio de IA; a tabela mostra
> **onde o esforço se concentra**.

| Frente | Esforço aprox. | Observação |
|--------|----------------|-----------|
| FT0 Fundação | ~1 | Maior risco é externo (Meta), não esforço. |
| FT1 Destino/localização | ~2 | Reusa Unidade/Endereço existentes. |
| FT2 Acompanhante | ~1,5 | Assento de acompanhante já existe. |
| **FT3 Geração (núcleo)** | **~5** | Maior bloco; Maps + Claude + fallback. |
| FT4 App motorista | ~3 | Evolui o `agente.app`. |
| FT5 Tempo real/puxar | ~2,5 | Reusa GPS + SignalR. |
| FT6 WhatsApp | ~2 | Cliente + webhook + templates. |
| FT7 App cidadão | ~1,5 | Fase 1 enxuta. |
| FT8 Painel | ~2 | Distribuído nas Fases 2–4. |
| FT9 Teste/piloto/implantação | ~3 | Fase 4. |

---

## 7. Fase 2 — backlog pós-piloto (após 13/07)

- Re-otimização contínua (replanejar com cancelamentos/atrasos em tempo real).
- App do cidadão completo (avaliação, histórico, notificações ricas).
- Dashboards gerenciais avançados (custo por rota, combustível, desempenho).
- Cutover/operação do WhatsApp oficial (se a verificação Meta não fechar até o D18).
- Componente financeiro do TFD (ajuda de custo/diárias).
- Integração com SISREG/regulação para puxar demanda automaticamente.
- Expansão para toda a frota (rollout municipal).

---

## 8. Marcos (resumo)

| Marco | Data | Significado |
|-------|------|-------------|
| **M1** | 28/06 (D5) | Destino + acompanhante + WhatsApp de confirmação |
| **M2** | 04/07 (D11) | Geração de translado ponta a ponta (núcleo) |
| **M3** | 10/07 (D17) | Fluxo operacional completo em teste |
| **M4** | 13/07 (D20) | **Piloto aceito** → Go/No-Go para expansão |
