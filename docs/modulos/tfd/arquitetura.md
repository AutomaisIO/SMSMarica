# Arquitetura Técnica — Módulo TFD

> Documento técnico que fundamenta o escopo e o cronograma. Respeita as regras
> não-negociáveis do projeto (ver [`CLAUDE.md`](../../../CLAUDE.md)): arquitetura de 3
> projetos, dois schemas (`smsmarica` pt-BR + `fhir` en), migrations imutáveis, erros via
> exceções tipadas, `agente.app` Android-only (ADR-0003).

---

## 1. Visão arquitetural

O TFD vive no schema **`smsmarica`** (é regra de negócio, não identidade clínica). A
identidade do paciente continua no **hub FHIR** (`fhir.patient`), referenciada por
`PacienteId` (Guid). Nada do transporte projeta para FHIR nesta fase.

```
  Painel React ──┐                          ┌── App Motorista (Flutter/Android, tablet)
                 │                           │
  App Cidadão ───┤      SMSMarica.Api        ├── WhatsApp (Meta Cloud API) [webhook+envio]
   (Flutter)     │  (Controllers + SignalR)  │
                 └──────────┬────────────────┘
                            │
                   SMSMarica.Core
        ┌───────────────────┼─────────────────────────┐
        │                   │                          │
  TransladoService   GeradorDeTransladoService   RastreamentoService
        │             (motor de otimização)            │
        │             ┌──────┴───────┐                 │
        ▼             ▼              ▼                  ▼
  SMSMais.Data   GoogleMaps     Claude          Job background
  (EF Core /       (Geocoding/    (ClaudeProvedorIa)  (GPS→chegada/ETA)
   PostgreSQL)     Routes/Matrix)
```

Os clientes externos (Google Maps, WhatsApp) seguem o **padrão existente**: `HttpClient`
tipado registrado em DI, `BaseUrl`/`Timeout` por configuração, **segredos cifrados** via
`IProtetorSegredos` + ASP.NET DataProtection (mesmo padrão de SISREG/Hub/Anthropic).

---

## 2. Modelo de dados — mudanças (migrations novas)

> Migrations são **imutáveis**: cada item abaixo é uma migration nova em
> `SMSMais.Data/Migrations/`. Nenhuma tabela existente é editada destrutivamente.

### 2.1 Destino e geolocalização (FT1)
- **`Unidade`** já tem `Endereco` (owned) + `Gps` (lat/long nullable). Mudanças:
  - tornar a geocodificação **efetiva** (preencher `Gps` ao salvar via Google Geocoding);
  - campo **`Municipio`/flag `Externa`** (destino fora de Maricá) — derivável de
    `Endereco.Cidade`, mas explicitar facilita filtros e regras.
- **Paciente (origem da coleta):** hoje `latitude/longitude` chegam como `0,0` no payload
  FHIR. Introduzir **cache de geocodificação local** no schema `smsmarica`
  (`tfd_geocodigo`: chave = hash do endereço normalizado → lat/long, fonte, geocodificado_em),
  evitando regeocodificar e mantendo o hub FHIR limpo. O `PacientesService` consulta esse
  cache para obter a coordenada de coleta.
- **Destino por sessão (opcional):** `SessaoDeTratamento` herda o destino do `Tratamento`
  (via `UnidadeId`). Permitir **override** por sessão só se houver demanda real — manter
  herança como padrão para não inflar o modelo.

### 2.2 Acompanhante confirmável (FT2)
- `SessaoDeTratamento` ganha **`AcompanhanteEsperado`** (`bool?`: null = não perguntado,
  true/false = confirmado) e **`AcompanhanteConfirmadoEm`/`Canal`** (WhatsApp/App/Manual).
  Os campos atuais `NomeAcompanhante`/`ParentescoAcompanhante` continuam para a execução.
- O assento de **acompanhante** já existe no veículo (`TipoAssento.Acompanhante`); a
  geração de rota só reserva quando `AcompanhanteEsperado = true`.

### 2.3 Rota ordenada e ETA (FT3, FT5)
- **`Alocacao`** ganha **`OrdemParada`** (int) e **`EtaPrevisto`/`JanelaInicio`/`JanelaFim`**
  (a sequência hoje é só visual).
- **`RotaDiaria`** ganha metadados da geração: **`Origem`** (`Manual`|`Gerada`),
  **`GeradaEm`**, **`DistanciaTotalMetros`**, **`DuracaoEstimadaSegundos`**, e o
  **`PlanoRotaJson`** (snapshot auditável do que o motor produziu: ordem, pernas,
  decisões do Claude).
- Distinguir **rota de coleta** vs **rota de entrega**: campo **`Fase`**
  (`Coleta`|`Entrega`|`Retorno`) em `RotaDiaria` **ou** sequência única com `OrdemParada`
  marcando tipo de parada (`Coleta`|`Destino`). Decisão registrada no ADR-0017.

### 2.4 "Puxar" pacientes e fila de espera (FT5)
- **`SessaoDeTratamento.Status`** ganha estados operacionais do retorno:
  `AguardandoRetorno` (paciente terminou o atendimento e espera carro). A query de "puxar"
  lista sessões `AguardandoRetorno` com a coordenada do destino e calcula **distância até o
  motorista** (último `PontoGps`).

### 2.5 WhatsApp (FT6)
- **`tfd_mensagem_whatsapp`**: id, sessao_id, paciente_id, template, direcao
  (`Saida`|`Entrada`), conteudo, status (`Enviada`|`Entregue`|`Lida`|`Falha`|`Recebida`),
  wa_message_id, ocorrido_em. Trilha de auditoria + idempotência do webhook.

---

## 3. Motor de geração de translado (FT3) — coração do módulo

`GeradorDeTransladoService.GerarAsync(DateOnly data)`:

**Entrada:** sessões elegíveis do dia (já existe `ListarSessoesElegiveis`), com
origem (coleta = endereço do paciente em Maricá) e destino (unidade do tratamento);
acompanhante esperado por sessão; frota disponível (veículos com mapa de assentos +
motoristas).

**Etapas:**

1. **Geocodificar** origens/destinos que faltam (cache `tfd_geocodigo` + Google Geocoding).
2. **Matriz de distâncias/tempos** entre pontos relevantes (Google **Distance Matrix**),
   com cache por par de coordenadas + horário aproximado.
3. **Distribuição nos carros (clustering + capacidade):**
   - heurística determinística agrupa por **proximidade de destino/região** e respeita
     **capacidade** (assentos disponíveis, assento de acompanhante quando esperado, tipo de
     veículo);
   - o **Claude** entra como camada de **raciocínio com restrições**: recebe os clusters
     candidatos, restrições de negócio (prioridades clínicas, horários de atendimento,
     pacientes que não podem viajar juntos, capacidade) e devolve, em **saída estruturada**
     (mesmo padrão do `ClaudeProvedorIa`), a **atribuição final paciente→veículo** com
     **justificativa** (auditável no `PlanoRotaJson`). Em caso de indisponibilidade da IA,
     a heurística determinística é o fallback.
4. **Sequenciamento (rota otimizada) por veículo:**
   - **Rota de coleta:** ordem ótima das paradas de coleta em Maricá → ponto de saída
     comum, via Google **Routes/Directions com otimização de waypoints** (resolve o TSP).
   - **Rota de entrega:** ordem ótima dos destinos (que podem ser cidades diferentes).
   - **Retorno:** simétrico, alimentado também pelo "puxar" (FT5).
5. **Persistir:** cria `RotaDiaria` (Origem=`Gerada`, distância/duração) + `Alocacao`
   ordenada (`OrdemParada`, `EtaPrevisto`) + `PlanoRotaJson`.

**Divisão de papéis (importante para custo e robustez):**
- **Google Maps** = verdade geográfica (distâncias, tempos, ordem ótima de waypoints).
- **Claude** = decisão de negócio (quem vai em qual carro, exceções, prioridades,
  explicação legível ao gestor).
- **Heurística determinística** = fallback e baseline (garante operação mesmo sem IA).

**Auditabilidade:** o gestor pode ver **por que** o sistema montou cada carro/rota
(`PlanoRotaJson`), e **editar manualmente** (a alocação manual existente continua válida).

---

## 4. Rastreamento em tempo real e "puxar" (FT5)

- **Ingestão de GPS** (já existe): `POST /rastreamento/pontos`. O app motorista passa a
  postar periodicamente em background.
- **Job background** (`IHostedService`): processa pontos novos →
  1. detecta cruzamento de **geofence** (residência/unidade) → grava `EventoChegada` (com
     debounce para não duplicar);
  2. atualiza posição corrente do motorista;
  3. recalcula **ETA** até a próxima parada (Distance Matrix com cache).
- **Tempo real:** **SignalR** (já disponível no projeto, hoje não usado aqui) publica
  posição/ETA para o **painel (mapa ao vivo)** e para o **app cidadão**.
- **"Puxar" pacientes:** endpoint lista sessões `AguardandoRetorno` (fora de Maricá) com a
  **distância** até cada motorista logado; o motorista aciona **"puxar (paciente +
  acompanhante)"** → cria/insere a `Alocacao` de retorno na rota dele, na ordem certa.

---

## 5. WhatsApp (FT6) — Meta Cloud API

- **Conta/identidade (pré-requisito, FT0):** **conta Meta Business em nome da Secretaria
  Municipal de Saúde de Maricá** + **WhatsApp Business Platform** (WABA) + número dedicado +
  nome de exibição. A **verificação de negócio** exige documentos do órgão (CNPJ, ato
  oficial, comprovante de endereço, site/e-mail `gov.br`, representante) e tem **lead time
  externo** — é o maior risco de prazo (ver `escopo.md` FT0/§9 e `cronograma.md` §5).
- **Cliente** `WhatsAppNotificador` implementando uma interface de notificação (estende o
  conceito do atual `INotificadorExame`/stub), `HttpClient` tipado, **segredos cifrados**.
- **Envio (outbound):** mensagens iniciadas pelo sistema usam **templates HSM aprovados**
  (confirmação de acompanhante, aviso de coleta com janela/ETA, lembrete).
- **Recebimento (inbound):** **webhook** `POST /integracoes/whatsapp/webhook` valida a
  assinatura da Meta, registra em `tfd_mensagem_whatsapp` (idempotente por `wa_message_id`)
  e roda a **máquina de estados de confirmação** (ex.: resposta "1 = com acompanhante / 2 =
  sem" → `SessaoDeTratamento.AcompanhanteEsperado`).
- **Janela de 24h / templates:** respeitar a regra de janela de serviço da Meta; fora da
  janela, só templates.

---

## 6. App do motorista (tablet) (FT4)

- Flutter, **Android-only** (ADR-0003), layout **tablet em paisagem**.
- **Rota do dia:** lista de paradas com ordem, paciente, endereço, janela/ETA; botão
  **navegar** (intent Google Maps/Waze).
- **Recebimento dinâmico:** novos pacientes liberados aparecem na fila (push via
  notificação + polling de fallback).
- **"Puxar":** tela com pacientes aguardando fora de Maricá + **distância**; ação puxar.
- **Embarque/desembarque:** marca status por parada; confirma chegada (geofence automática
  ou manual).
- **GPS em background:** `geolocator` + serviço em foreground (já há fluxo de permissões);
  posta pontos periodicamente; **tolerante a offline** (enfileira e sincroniza).

---

## 7. App do cidadão (paciente) (FT7)

- Flutter (Android + iOS). Telas novas: **meus translados** (agenda), **confirmar
  acompanhante**, **acompanhar chegada (ETA em tempo real)** via SignalR. Avaliação
  completa → Fase 2.

---

## 8. Painel do gestor (FT8)

- React + Vite. Botão **"Gerar translado"** (chama o motor), **mapa de rotas**
  (Google Maps JS / react-leaflet), **frota ao vivo** (SignalR), destino na tela de
  tratamento, **geocodificação** ao salvar unidade, **fila de "puxar"** e indicadores
  (ocupação, atrasos).

---

## 9. Conformidade e segurança

- **Schemas:** tudo em `smsmarica` (pt-BR). Sem FK cross-schema nova além do padrão
  `smsmarica → fhir` (PacienteId).
- **Erros:** exceções tipadas (`NaoEncontradoException`/`ConflitoException`/
  `ValidacaoException`) → `ProblemDetails`.
- **RBAC:** novos endpoints com `[RequerPermissao(...)]`; módulos de permissão
  sincronizados (front + back) — ver skill `sincronizar-permissoes`.
- **LGPD:** coordenadas de pacientes e mensagens WhatsApp têm finalidade (execução do TFD)
  e **política de retenção** (definir janela de expurgo dos pontos GPS e mensagens);
  acesso auditado.
- **Segredos:** chaves Google/Meta **cifradas** (`IProtetorSegredos`), nunca em
  `appsettings` versionado.
- **Acesso DATASUS/SISREG:** habilitação institucional — **ofício do Secretário nomeando o
  responsável técnico** (documentos já enviados à Avante). Pré-requisito da integração de
  regulação (ADR-0012). Ref.: `servicos-datasus.saude.gov.br`.

---

## 10. ADR-0017 proposto (a registrar em FT0)

**Título:** *Geração inteligente de translado TFD — otimização de rota com Google Maps +
Claude, mantendo alocação manual.*

**Decisão (rascunho):**
- Google Maps é a fonte de verdade geográfica; Claude decide a distribuição nos carros com
  restrições de negócio; existe **heurística determinística de fallback**.
- A geração **não substitui** a alocação manual existente — produz um plano editável e
  auditável (`PlanoRotaJson`).
- Geocodificação fica em **cache local** (`tfd_geocodigo`) no schema `smsmarica`; o hub
  FHIR não é alterado.
- WhatsApp via **Meta Cloud API** com templates HSM e webhook idempotente.

**Consequências:** custo de APIs externas (mitigado por cache), dependência de verificação
Meta (lead time), necessidade de retenção LGPD para GPS/mensagens.
