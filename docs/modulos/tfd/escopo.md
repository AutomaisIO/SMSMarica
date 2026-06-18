# Escopo do Projeto — Módulo TFD (Tratamento Fora do Domicílio)

| | |
|---|---|
| **Projeto** | Módulo TFD — Transporte sanitário de pacientes |
| **Plataforma** | SMSMarica (monorepo: backend .NET 10 + painel React + apps Flutter + hub FHIR) |
| **Cliente** | Secretaria Municipal de Saúde de Maricá (SMS Maricá) |
| **Operadora (OS)** | Avante — A/C Sra. Clarisse |
| **Equipe** | Equipes dedicadas: desenvolvimento, implantação e (pós) suporte, com apoio de IA |
| **Janela** | **20 dias corridos** (24/06 → 13/07/2026) até o piloto operacional — ver [`cronograma.md`](./cronograma.md) |
| **Data** | 2026-06-18 · v1 (para aprovação) |

---

## 1. Contexto e objetivo

O **TFD (Tratamento Fora do Domicílio)** é a política do SUS que garante transporte —
e, quando indicado, acompanhante e ajuda de custo — a pacientes que precisam de
atendimento **fora do município de origem**, porque o procedimento não existe ou não
está disponível na rede local. Em Maricá, isso significa **levar pacientes da cidade até
hospitais e unidades de referência em outras cidades** (consultas, exames de alta
complexidade, hemodiálise, radioterapia, oncologia, etc.) e **trazê-los de volta**, todos
os dias, com **vários carros, vários motoristas e vários destinos**.

**Objetivo do projeto:** transformar o planejamento e a execução do transporte TFD — hoje
manual e dependente de planilhas/experiência do regulador — em um processo **inteligente,
rastreável e comunicado**, que:

1. saiba **para onde** cada paciente vai (destino por tratamento);
2. saiba **quem leva acompanhante**, confirmado pelo próprio paciente;
3. **monte os carros e as rotas automaticamente** (coleta e entrega otimizadas) com Google
   Maps + Claude;
4. entregue ao **motorista, no tablet**, a lista certa, na ordem certa, com a rota pronta;
5. **converse com o paciente pelo WhatsApp** (confirmação, horário, chegada);
6. permita ao motorista **"puxar"** pacientes que estão aguardando (inclusive os que já
   estão fora de Maricá, vendo a **distância** de cada um);
7. dê ao gestor **visão ao vivo** da operação.

---

## 2. Problema atual (dores)

- **Planejamento manual e demorado:** distribuir dezenas de pacientes em carros e definir
  a ordem das paradas é feito "na mão", sujeito a erro e retrabalho.
- **Rotas não otimizadas:** carros fazem trajetos mais longos do que o necessário, gastando
  combustível e tempo, e atrasando pacientes.
- **Acompanhante descoberto na hora:** sem confirmação prévia, o carro pode chegar e faltar
  (ou sobrar) assento.
- **Paciente sem informação:** não sabe o horário de coleta nem onde o carro está.
- **Volta desorganizada:** pacientes esperam horas após o atendimento porque não há um
  mecanismo que case quem terminou com o carro mais próximo.
- **Pouca rastreabilidade:** difícil saber em tempo real onde estão os veículos e quem
  ainda falta buscar.

---

## 3. Visão da solução

```
                       ┌──────────────────────────────────────────────┐
   Cadastros           │   Painel do gestor (web)                       │
  (já existem,    ───▶ │  destino do tratamento · gerar translado (IA)  │
   com ajustes)        │  rotas no mapa · frota ao vivo · "puxar" fila  │
                       └───────────────┬────────────────────────────────┘
                                       │
            ┌──────────────────────────┼───────────────────────────┐
            ▼                          ▼                           ▼
   ┌────────────────┐        ┌──────────────────┐        ┌──────────────────┐
   │  Google Maps    │       │   Backend .NET    │        │   Claude (IA)     │
   │ geocode/rotas/  │◀────▶ │  motor de geração │ ◀────▶ │ distribuição nos  │
   │ distância       │       │  de translado     │        │ carros + decisões │
   └────────────────┘        └───────┬──────────┘        └──────────────────┘
                                      │
              ┌───────────────────────┼───────────────────────┐
              ▼                       ▼                       ▼
     ┌─────────────────┐    ┌──────────────────┐    ┌──────────────────┐
     │ App do motorista │    │   WhatsApp        │    │ App do cidadão    │
     │ (tablet, Android)│    │ (Meta Cloud API)  │    │ (paciente)        │
     │ rota+puxar+GPS   │    │ confirma/avisa     │    │ confirma/acompanha│
     └─────────────────┘    └──────────────────┘    └──────────────────┘
```

A solução **reaproveita a plataforma existente** (ver §4) e adiciona três integrações
externas: **Google Maps** (geolocalização e rotas reais), **Claude** (distribuição
inteligente e decisões com restrições) e **WhatsApp oficial** (comunicação com o paciente).

---

## 4. Estado atual da plataforma — o que já existe vs. o que é novo

> Esta seção é a base honesta do cronograma: boa parte da fundação **já está construída**,
> o que torna a janela de 20 dias viável para um piloto.

| Capacidade | Estado hoje | Classificação |
|------------|-------------|---------------|
| Cadastro de **motoristas, veículos, unidades, tratamentos, pacientes** | ✅ Completo (CRUD no painel + backend) | Ajuste pontual |
| **Mapa de assentos por veículo** (fileiras/assentos, tipo "Acompanhante", bloqueio) | ✅ Completo | Reuso |
| **Rota diária** (veículo + motorista + data + status) e **alocação** de paciente em assento | ✅ Manual, com validações | Evoluir |
| **Tratamento → Unidade de destino** (unidade tem endereço completo + lat/long opcional) | 🟡 Existe, mas o destino não é tratado como tal nem é sempre geolocalizado | Ajuste |
| **Sessões de tratamento** com periodicidade, acompanhante (texto livre) e ida/volta | 🟡 Acompanhante só registrado na execução, não confirmado antes | Evoluir |
| **Ingestão de GPS do motorista, geofences e eventos de chegada** | 🟡 Recebe pontos; falta cálculo automático de chegada/ETA e tempo real | Evoluir |
| **App do motorista** (Flutter, Android) | 🟡 Scaffold: login + lista de paradas (mock); falta GPS em background, navegação, layout tablet, "puxar" | Desenvolver |
| **App do cidadão** (Flutter) | 🟡 Scaffold: login + perfil; falta translados/ETA/confirmação | Desenvolver |
| **Claude / IA** no backend | ✅ Integrado (geração estruturada + cache de prompt) | Reuso |
| **Geocodificação de endereços** (paciente/unidade) | ❌ Paciente sem coordenadas (lat/long = 0,0); sem Google Geocoding | Novo |
| **Otimização de rota** (Google Maps Directions/Distance Matrix/Routes) | ❌ Inexistente | Novo |
| **Distribuição automática nos carros** | ❌ Inexistente (hoje é manual) | Novo |
| **"Puxar" pacientes / distância em tempo real** | ❌ Inexistente | Novo |
| **WhatsApp** | ❌ Apenas um stub de log; sem envio real | Novo |
| **Tempo real (mapa ao vivo / push de ETA)** | ❌ Inexistente (infra SignalR disponível no projeto) | Novo |

**Conclusão:** ~50% da fundação de dados e telas já existe. O esforço novo concentra-se
em **3 integrações externas** (Maps, WhatsApp, e o uso do Claude já disponível) e na
**evolução dos apps**.

---

## 5. Escopo funcional — 10 Frentes de Trabalho (FT)

Cada frente lista **objetivo**, **estado atual** e **entregável**. Os requisitos numerados
e critérios de aceite estão em [`requisitos.md`](./requisitos.md); o desenho técnico em
[`arquitetura.md`](./arquitetura.md).

### FT0 — Fundação, contas e credenciais
- **Objetivo:** habilitar as integrações externas e registrar a decisão arquitetural.
- **Inclui:** conta **Google Cloud** com Maps Platform (Geocoding, Distance Matrix,
  Routes/Directions com otimização de waypoints); **criação da conta Meta Business em nome
  da Secretaria Municipal de Saúde de Maricá** + **verificação de negócio** (WhatsApp
  Business Platform) e criação dos **templates de mensagem (HSM)**; registro do **ADR-0017**
  (otimização de rota TFD); credenciais cifradas no backend (padrão `IProtetorSegredos`).
- **Documentos da Secretaria para a verificação Meta** (fornecidos pela SMS/OS): **CNPJ**
  ativo; documento oficial que comprove **nome e CNPJ** (ato de criação/publicação);
  **comprovante de endereço** do órgão; **site oficial** (domínio `gov.br`) e **e-mail** do
  mesmo domínio; **telefone institucional** e dados do **representante** responsável.
- **Acesso às APIs do SUS (DATASUS):** habilitação institucional para integração com a
  **regulação/SISREG** (caminho para puxar a demanda de TFD em fase posterior). Requer
  **ofício do Secretário de Saúde nomeando o responsável técnico** pelo acesso. **Os
  documentos necessários já foram enviados à Avante**; pendente o ofício de nomeação.
  Referência: `servicos-datasus.saude.gov.br`.
- **Entregável:** ambientes/credenciais provisionados, conta Meta da Secretaria em
  verificação, acesso DATASUS encaminhado, ADR-0017 registrado, templates WhatsApp
  submetidos à Meta.

### FT1 — Destino do tratamento + geocodificação
- **Objetivo:** cada tratamento sabe **para onde** o paciente vai, com coordenadas reais.
- **Estado atual:** o tratamento já aponta para uma Unidade com endereço; falta tratá-la
  como destino canônico, suportar **destino fora de Maricá** e **geocodificar** endereços.
- **Inclui:** geocodificação automática (Google) ao salvar Unidade/paciente; marcação de
  unidade interna/externa ao município; fila de revisão para endereços que não
  geocodificam; destino visível na tela de tratamento (com mapa).
- **Entregável:** destino geolocalizado por tratamento; endereços de pacientes e unidades
  com lat/long; tela de tratamento mostrando o destino.

### FT2 — Acompanhante (planejamento + confirmação)
- **Objetivo:** saber, **antes do dia**, se o paciente vai **com ou sem acompanhante**.
- **Estado atual:** assento de acompanhante já existe no veículo; o acompanhante hoje só é
  registrado na execução, sem confirmação prévia.
- **Inclui:** campo "vai ter acompanhante?" por sessão; **confirmação pelo paciente via
  WhatsApp** (pergunta automática N dias/horas antes); reserva automática do assento de
  acompanhante quando confirmado.
- **Entregável:** estado de acompanhante confirmado por sessão, alimentando a geração de
  rotas (capacidade dos carros).

### FT3 — Motor de geração de translado (Google Maps + Claude) ⭐ núcleo
- **Objetivo:** com um clique, **distribuir os pacientes nos carros** e gerar **rotas
  otimizadas de coleta e de entrega**.
- **Estado atual:** alocação e ordenação são manuais.
- **Inclui:**
  - **Distribuição nos carros** respeitando capacidade, tipo de assento, acompanhante e
    agrupamento por destino/região (Claude + heurística de capacidade).
  - **Rota de coleta otimizada** (pegar todos os pacientes em Maricá na melhor ordem) com
    Google Maps (matriz de distâncias + otimização de waypoints).
  - **Rota de entrega otimizada** (levar cada paciente ao seu destino) e a volta.
  - Persistência: cria as **rotas diárias** e a **alocação ordenada** (ordem de parada,
    janelas e ETA) prontas para o motorista.
- **Entregável:** endpoint `POST /translados/gerar` + botão **"Gerar translado"** no painel,
  produzindo rotas otimizadas auditáveis.

### FT4 — App do motorista (tablet)
- **Objetivo:** o motorista executa a rota a partir do **tablet** (Android).
- **Estado atual:** app scaffoldado (login + lista mock).
- **Inclui:** layout **tablet** (paisagem); recebimento da **rota do dia** (pacientes a
  coletar em Maricá, em ordem, com a rota); **navegação** (abrir Google Maps/Waze);
  **recebimento dinâmico** de novos pacientes conforme são **liberados**; **"puxar"
  pacientes que estão aguardando**; **embarque/desembarque** e status por parada; **GPS em
  background** alimentando o rastreamento.
- **Entregável:** app do motorista operacional para o piloto.

### FT5 — Rastreamento em tempo real, distância e "puxar pacientes"
- **Objetivo:** saber onde está cada carro e **casar pacientes que terminaram com o carro
  mais próximo**.
- **Estado atual:** ingestão de GPS existe; falta processamento, distância/ETA e tempo real.
- **Inclui:** processamento dos pontos GPS → **eventos de chegada** (geofence) automáticos;
  cálculo de **distância/ETA** até pacientes; **lista de pacientes aguardando fora de
  Maricá com a distância** para cada motorista, com botão **"puxar (paciente +
  acompanhante)"**; **push em tempo real** (mapa ao vivo no painel; ETA no app cidadão).
- **Entregável:** rastreamento ao vivo + mecanismo de "puxar" por proximidade.

### FT6 — WhatsApp do paciente (Meta Cloud API)
- **Objetivo:** falar com o paciente pelo canal que ele usa.
- **Estado atual:** apenas um stub de log.
- **Pré-requisito:** **conta Meta Business da Secretaria** verificada (ver FT0 e §8) — é o
  item de maior lead time externo.
- **Inclui:** cliente WhatsApp oficial (Meta Cloud API) com **templates aprovados**;
  fluxos de **confirmação de acompanhante**, **aviso de coleta + janela/horário**,
  **lembrete** e (opcional) **avaliação**; **webhook** que recebe a resposta do paciente e
  atualiza a sessão.
- **Entregável:** comunicação automática paciente↔sistema via WhatsApp.

### FT7 — App do cidadão (paciente)
- **Objetivo:** dar autonomia e transparência ao paciente.
- **Estado atual:** app scaffoldado (login + perfil).
- **Inclui:** ver os **translados/sessões** agendados; **confirmar acompanhante** pelo app
  (alternativa ao WhatsApp); ver o **carro chegando em tempo real** (ETA); **avaliar** o
  serviço após a viagem.
- **Entregável (Fase 1):** ver agenda + confirmar acompanhante + ETA. Avaliação completa
  pode entrar na Fase 2 (ver §6).

### FT8 — Painel do gestor (web)
- **Objetivo:** operar e supervisionar o TFD.
- **Estado atual:** telas de translado/tratamento/unidade existem; sem geração IA, mapa de
  rota ou tempo real.
- **Inclui:** botão **"Gerar translado"**; visualização das **rotas no mapa**; **frota ao
  vivo**; destino na tela de tratamento; geocodificação nas unidades; **fila de pacientes
  aguardando** ("puxar"); indicadores operacionais (ocupação, atrasos).
- **Entregável:** painel TFD completo para o piloto.

### FT9 — Qualidade, piloto, treinamento e implantação
- **Objetivo:** garantir que funciona com pacientes reais.
- **Inclui:** testes automatizados (backend) e roteiros manuais (apps); **piloto assistido**
  com 1–2 veículos e um conjunto de pacientes reais; **treinamento** de motoristas,
  reguladores de TFD e atendentes; **cutover** do WhatsApp para o número oficial;
  monitoramento pós-go-live.
- **Entregável:** piloto operando + plano de expansão para toda a frota.

---

## 6. Fora de escopo desta fase (Fase 2 — backlog pós-piloto)

Para caber na janela de 20 dias com qualidade, os itens abaixo ficam para a **Fase 2**
(após o piloto), **sem sair do escopo geral** do módulo:

- **Otimização avançada multi-dia e re-otimização contínua** (replanejar em tempo real
  quando há cancelamento/atraso) — a Fase 1 entrega geração diária + "puxar" manual.
- **App do cidadão completo** (avaliação com nota/comentário, histórico, notificações push
  ricas) — Fase 1 entrega confirmação + ETA.
- **Dashboards gerenciais avançados** (BI de custos por rota/combustível, indicadores de
  desempenho por motorista).
- **Ajuda de custo / diárias do TFD** (componente financeiro da política, além do
  transporte).
- **Integração com SISREG/escala de regulação** para puxar automaticamente a demanda de
  TFD (hoje a demanda entra pelos tratamentos cadastrados).
- **Conformidade DICOM/PACS, FHIR clínico** — não pertencem ao transporte.

---

## 7. Entregáveis do projeto (Fase 1)

1. **Backend .NET** com: destino geolocalizado, acompanhante confirmável, motor de geração
   de translado (Maps + Claude), rastreamento em tempo real, cliente WhatsApp e endpoints
   para os apps.
2. **Painel web** com geração de translado, mapa de rotas, frota ao vivo e fila de "puxar".
3. **App do motorista (tablet, Android)** operacional.
4. **App do cidadão** com agenda, confirmação de acompanhante e ETA (Fase 1).
5. **Integração WhatsApp** (Meta Cloud API) com templates aprovados.
6. **Documentação**: ADR-0017, este escopo, requisitos, arquitetura, cronograma e
   apresentação.
7. **Piloto assistido** + material de treinamento + plano de expansão.

---

## 8. Premissas

- **Equipe:** equipes dedicadas — **desenvolvimento**, **implantação** e (pós-go-live) **suporte** —, com apoio intensivo de IA na implementação.
- **Janela:** 20 dias corridos (24/06 → 13/07/2026, início quarta-feira) para o **piloto
  operacional** — não para a operação em escala plena, que segue na expansão pós-piloto.
- **Reuso:** a fundação existente (cadastros, rota diária, assentos, GPS, Claude, apps
  scaffoldados) está estável e disponível.
- **Acessos:** a SMS/OS provê em tempo hábil: conta/forma de pagamento para Google Cloud e
  Meta; **documentos da Secretaria para a verificação de negócio da Meta** (CNPJ, ato
  oficial, comprovante de endereço, site/e-mail `gov.br`, representante); **ofício do
  Secretário nomeando o responsável técnico para acesso às APIs do SUS (DATASUS)** (documentos
  já enviados à Avante); número de telefone para o WhatsApp Business; e 1–2 motoristas +
  tablets para o piloto.
- **Dados:** endereços de pacientes e unidades estão razoavelmente completos (CEP +
  logradouro/número) para permitir geocodificação; lacunas entram na fila de revisão.
- **LGPD:** o tratamento de localização e mensagens segue a base legal de execução de
  política pública de saúde; coordenadas e mensagens têm finalidade e retenção definidas.

---

## 9. Riscos e mitigações

| # | Risco | Impacto | Mitigação |
|---|-------|---------|-----------|
| R1 | **Criação da conta Meta da Secretaria + verificação de negócio** (WhatsApp oficial) tem lead time externo (dias a semanas) e está no caminho crítico | Alto | Iniciar **no D1** como trilha paralela, com os documentos da Secretaria prontos; **piloto com número de teste/sandbox** da Meta enquanto a verificação não conclui; cutover para o número oficial no D18 ou início da Fase 2. |
| R2 | **Qualidade dos endereços** (geocodificação falha) | Médio | Fila de revisão + **pin manual no mapa**; usar CEP como fallback; não bloquear a geração de rota. |
| R3 | **Janela de 20 dias corridos agressiva** | Alto | Equipes dedicadas (desenvolvimento e implantação); reuso máximo da fundação; desenvolvimento assistido por IA; **piloto com 1–2 veículos**; itens de menor risco/valor na Fase 2 (§6). |
| R5 | **Conectividade do tablet** em trajeto (zonas sem sinal) | Médio | App tolerante a offline: baixa a rota antes de sair; enfileira GPS/eventos e sincroniza ao reconectar. |
| R6 | **Aceitação dos motoristas** ao app | Médio | Layout tablet simples, botões grandes; treinamento; piloto assistido com feedback. |

---

## 10. Equipe do projeto

O projeto é conduzido por **equipes dedicadas**, que atuam em momentos distintos do
cronograma — do desenvolvimento à implantação assistida e ao suporte contínuo após a
entrada em operação.

| Equipe | Quando atua | Responsabilidade |
|--------|-------------|------------------|
| **Desenvolvimento** | Fases 1–3 (24/06–10/07) | Backend, painel, apps (motorista e cidadão) e integrações (Google Maps, IA, WhatsApp). |
| **Implantação** | Fase 4 (11–13/07) | Testes, treinamento (motoristas, reguladores, atendentes), piloto assistido e entrada em operação. |
| **Suporte** | Após o piloto (contínuo) | Sustentação, monitoramento, atendimento ao usuário e evolução contínua (Fase 2). |
| **Patrocínio / decisão** | Todo o projeto | **SMS Maricá** — aprovação de escopo, acessos, prioridades. |
| **Gestão operacional / TFD** | Todo o projeto | **OS Avante (A/C Sra. Clarisse)** — regras de negócio, dados, motoristas/veículos do piloto, validação. |

As equipes trabalham de forma **encadeada**: desenvolvimento entrega → implantação coloca em
operação → suporte garante continuidade e evolução.

---

## 11. Critérios de aceite (resumo)

O piloto é considerado **aceito** quando, com pacientes e veículos reais:

1. O gestor gera o translado do dia com **um clique** e obtém rotas de **coleta e entrega**
   otimizadas e auditáveis (**FT3**).
2. O paciente recebe e responde a confirmação de **acompanhante via WhatsApp**, e o assento
   é reservado corretamente (**FT2, FT6**).
3. O motorista executa a rota pelo **tablet**, recebe pacientes liberados e **"puxa"** um
   paciente aguardando (**FT4, FT5**).
4. O gestor vê a **frota ao vivo** e a **distância** dos pacientes aguardando (**FT5, FT8**).
5. O paciente acompanha a **chegada do carro (ETA)** pelo app (**FT7**).

Critérios detalhados por frente em [`requisitos.md`](./requisitos.md).

---

## 12. Referências

- Decisões arquiteturais: [`docs/adr/`](../../adr/) — em especial 0003 (agente.app
  Android-only), 0004 (3 projetos), 0007 (schema FHIR), 0010 (hub FHIR autônomo), 0011
  (módulo IA), 0013 (agenda multi-recurso). **ADR-0017** (otimização de rota TFD) a ser
  registrado em FT0.
- Documentação canônica: [`docs/architecture.md`](../../architecture.md),
  [`docs/domain.md`](../../domain.md), [`docs/database.md`](../../database.md),
  [`docs/roadmap.md`](../../roadmap.md) (marcos M3 Operação, M4 Rastreamento, M5 Avaliação,
  M7 Integrações).
- Demais documentos do módulo: [`arquitetura.md`](./arquitetura.md),
  [`requisitos.md`](./requisitos.md), [`cronograma.md`](./cronograma.md),
  [`apresentacao.md`](./apresentacao.md).
