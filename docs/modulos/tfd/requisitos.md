# Requisitos — Módulo TFD

> Requisitos funcionais (RF) e não-funcionais (RNF) numerados, critérios de aceite por
> frente e matriz de rastreabilidade. Complementa o [`escopo.md`](./escopo.md) (visão
> de cliente) e a [`arquitetura.md`](./arquitetura.md) (como será construído).
>
> Frentes de Trabalho (FT0–FT9) definidas no [`escopo.md`](./escopo.md) §5.

---

## 1. Requisitos funcionais (RF)

### FT1 — Destino do tratamento + geocodificação
- **RF1.1** Todo tratamento deve ter uma **unidade de destino** com endereço completo.
- **RF1.2** O sistema deve **geocodificar** (lat/long) o endereço da unidade e do paciente
  automaticamente ao salvar, usando Google Geocoding, com **cache** para não regeocodificar.
- **RF1.3** Endereços que não geocodificarem entram em uma **fila de revisão**; o gestor
  pode **fixar a coordenada manualmente no mapa (pin)**.
- **RF1.4** O sistema deve distinguir destino **dentro** vs **fora de Maricá**.
- **RF1.5** A tela de tratamento deve exibir o **destino e sua localização no mapa**.

### FT2 — Acompanhante
- **RF2.1** Cada sessão deve registrar se o paciente **terá acompanhante** (sim/não/não
  perguntado).
- **RF2.2** O sistema deve **perguntar ao paciente, via WhatsApp**, se haverá acompanhante,
  N horas/dias antes da sessão (configurável).
- **RF2.3** A resposta do paciente atualiza automaticamente a sessão e o **canal** da
  confirmação (WhatsApp/App/Manual).
- **RF2.4** Quando há acompanhante confirmado, a geração de rota **reserva o assento de
  acompanhante** no veículo.
- **RF2.5** O gestor pode **ajustar manualmente** o acompanhante (override).

### FT3 — Geração de translado (Maps + Claude)
- **RF3.1** O gestor gera o translado de uma data com **uma ação** ("Gerar translado").
- **RF3.2** O sistema **distribui os pacientes nos carros** respeitando capacidade, tipo de
  assento, acompanhante e agrupamento por destino/região.
- **RF3.3** O sistema calcula a **rota de coleta otimizada** (pegar todos em Maricá na
  melhor ordem).
- **RF3.4** O sistema calcula a **rota de entrega otimizada** (levar cada um ao destino) e o
  **retorno**.
- **RF3.5** Cada rota gerada registra **ordem de parada, ETA por parada, distância e duração
  total**.
- **RF3.6** O plano gerado é **auditável** (por que cada carro/rota foi montado) e
  **editável** manualmente (a alocação manual existente continua válida).
- **RF3.7** Em indisponibilidade da IA, o sistema usa **fallback determinístico** e gera
  rota mesmo assim.

### FT4 — App do motorista (tablet)
- **RF4.1** O motorista autentica e vê **a rota do dia** com paradas em ordem
  (paciente, endereço, janela/ETA).
- **RF4.2** O motorista abre a **navegação** (Google Maps/Waze) para a próxima parada.
- **RF4.3** O app recebe **dinamicamente** novos pacientes conforme são **liberados**.
- **RF4.4** O motorista pode **"puxar" pacientes que estão aguardando**.
- **RF4.5** O motorista marca **embarque/desembarque** e **chegada** por parada.
- **RF4.6** O app posta **GPS periodicamente em background** e é **tolerante a offline**
  (baixa a rota, enfileira eventos, sincroniza ao reconectar).
- **RF4.7** Layout **tablet** (paisagem, botões grandes).

### FT5 — Rastreamento, distância e "puxar"
- **RF5.1** O sistema processa os pontos GPS e gera **eventos de chegada** automáticos
  (geofence), sem duplicar.
- **RF5.2** O sistema calcula **distância/ETA** do veículo até as próximas paradas.
- **RF5.3** O sistema lista os **pacientes aguardando retorno fora de Maricá**, com a
  **distância** até cada motorista.
- **RF5.4** O motorista/gestor pode **puxar** um paciente aguardando (+ acompanhante), que é
  inserido na rota de retorno na ordem correta.
- **RF5.5** Painel e app cidadão recebem **atualização em tempo real** (posição/ETA).

### FT6 — WhatsApp (Meta Cloud API)
- **RF6.1** Enviar mensagens via **templates aprovados** (confirmação, aviso de coleta com
  janela/horário, lembrete).
- **RF6.2** Receber respostas via **webhook** e atualizar a sessão correspondente.
- **RF6.3** Registrar **toda mensagem** (entrada/saída, status) para auditoria; webhook
  **idempotente**.
- **RF6.4** Respeitar a **janela de serviço** da Meta e as regras de template.

### FT7 — App do cidadão (Fase 1)
- **RF7.1** Ver os **translados/sessões** agendados.
- **RF7.2** **Confirmar acompanhante** pelo app (alternativa ao WhatsApp).
- **RF7.3** Acompanhar a **chegada do carro (ETA) em tempo real**.
- *(Fase 2: avaliação com nota/comentário, histórico, notificações ricas.)*

### FT8 — Painel do gestor
- **RF8.1** Botão **"Gerar translado"** e visualização do resultado.
- **RF8.2** **Mapa das rotas** geradas (coleta/entrega).
- **RF8.3** **Frota ao vivo** no mapa.
- **RF8.4** **Fila de pacientes aguardando** com distância ("puxar").
- **RF8.5** Indicadores operacionais (ocupação dos carros, atrasos).
- **RF8.6** Geocodificação ao salvar unidade + fila de revisão de endereços.

### FT0 / FT9 — Fundação e Implantação
- **RF0.1** Credenciais Google/Meta provisionadas e **cifradas**.
- **RF0.2** Templates WhatsApp submetidos e aprovados pela Meta.
- **RF0.3** **Conta Meta Business em nome da Secretaria de Saúde de Maricá** criada e
  submetida à **verificação de negócio** com os documentos do órgão (CNPJ, ato oficial,
  comprovante de endereço, site/e-mail `gov.br`, representante).
- **RF0.4** **Acesso às APIs do SUS (DATASUS)** habilitado via **ofício do Secretário
  nomeando o responsável técnico** (documentos já enviados à Avante) — pré-requisito da
  integração com a regulação/SISREG. Ref.: `servicos-datasus.saude.gov.br`.
- **RF9.1** Roteiro de **piloto assistido** (1–2 veículos) executado com pacientes reais.
- **RF9.2** **Treinamento** de motoristas, reguladores e atendentes.
- **RF9.3** **Cutover** do WhatsApp para o número oficial.

---

## 2. Requisitos não-funcionais (RNF)

- **RNF1 — Desempenho:** gerar o translado de um dia (~150 sessões) em **tempo aceitável
  para uso interativo** (meta: < ~30 s), usando cache de geocodificação/matrizes.
- **RNF2 — Tempo real:** atualização de posição/ETA no painel/app em poucos segundos
  (SignalR).
- **RNF3 — Disponibilidade/robustez:** o motor de geração **degrada com elegância**
  (fallback determinístico) se Google/Claude estiverem indisponíveis; o app motorista opera
  **offline** com sincronização posterior.
- **RNF4 — Eficiência de integrações:** uso de APIs externas **monitorado** e **cacheado**;
  sem chamadas redundantes.
- **RNF5 — Segurança/LGPD:** segredos cifrados; coordenadas e mensagens com finalidade e
  **retenção definida**; acesso por RBAC e auditado.
- **RNF6 — Conformidade arquitetural:** schema `smsmarica`, migrations imutáveis, exceções
  tipadas, ADR-0017 registrado, `agente.app` Android-only.
- **RNF7 — Usabilidade (motorista):** tablet, paisagem, botões grandes, mínimo de toques
  para a tarefa do dia.

---

## 3. Critérios de aceite por frente (piloto)

| FT | Critério de aceite (verificável no piloto) |
|----|--------------------------------------------|
| FT1 | Tratamentos do piloto têm destino com lat/long; ≥ 90% dos endereços geocodificados automaticamente; os demais com pin manual. |
| FT2 | Pacientes do piloto recebem a pergunta de acompanhante por WhatsApp e a resposta reflete no assento reservado. |
| FT3 | "Gerar translado" produz, para o dia, carros montados + rotas de coleta e entrega otimizadas, auditáveis e editáveis. |
| FT4 | Motorista executa a rota pelo tablet, recebe um paciente liberado e puxa um aguardando; GPS chega ao backend. |
| FT5 | Painel mostra frota ao vivo; lista de aguardando exibe distância correta; "puxar" insere o paciente na rota. |
| FT6 | Mensagens enviadas por template e respostas recebidas pelo webhook atualizam a sessão (registro auditável). |
| FT7 | Paciente vê sua agenda, confirma acompanhante e acompanha o ETA no app. |
| FT8 | Gestor opera o dia inteiro pelo painel (gerar, ajustar, acompanhar, puxar). |
| FT9 | Piloto concluído com 1–2 veículos; equipe treinada; plano de expansão aprovado. |

---

## 4. Matriz de rastreabilidade (RF → FT → entregável)

| Necessidade do cliente | RF | FT | Entregável |
|------------------------|-----|----|-----------|
| Destino para cada tratamento | RF1.1–RF1.5 | FT1 | Destino geolocalizado + tela |
| Com/sem acompanhante (via WhatsApp) | RF2.1–RF2.5, RF6.1–RF6.2 | FT2, FT6 | Acompanhante confirmável |
| Distribuir nos carros + rotas otimizadas (Maps+Claude) | RF3.1–RF3.7 | FT3 | Motor de geração + botão no painel |
| App do motorista no tablet | RF4.1–RF4.7 | FT4 | App tablet operacional |
| Receber pacientes liberados + puxar aguardando | RF4.3–RF4.4, RF5.3–RF5.4 | FT4, FT5 | Fila dinâmica + "puxar" |
| Ver distância de cada paciente fora de Maricá | RF5.2–RF5.3 | FT5 | Lista com distância/ETA |
| Falar com paciente no WhatsApp | RF6.1–RF6.4 | FT6 | Integração Meta Cloud API |
| App do paciente com mais informações | RF7.1–RF7.3 | FT7 | App cidadão (Fase 1) |
| Cadastrar motoristas/carros/pacientes/unidades/tratamentos/endereços | (base existente + RF1.x) | FT1, FT8 | Ajustes nos cadastros + geocodificação |
| Gerar translado e acompanhar a operação | RF3.1, RF8.1–RF8.6 | FT3, FT8 | Painel TFD |
