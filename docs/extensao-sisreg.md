# Extensão SISREG — ponte de eventos em tempo (quase) real

> Estado: **fase 1 em construção** (marcação + cancelamento). Documento vivo — atualizar quando
> novas requisições do SISREG forem reveladas/tratadas.

## Objetivo

Atualizar a base do SMSMarica **a partir do que os operadores fazem no SISREG**, antecipando o que
hoje só chega pela **varredura diária** (arquivo TXT / `cons_agendas`). A fonte é a extensão de
navegador [`SMSMais.chrome`](../SMSMais.chrome/README.md), que **só observa** o SISREG — nunca
dispara requisição para lá, então **não gasta o orçamento anti-robô** e não derruba sessão. Tudo
que ela registra foi o próprio operador quem fez, com a senha dele.

Complementa (não substitui) a varredura: os eventos dão o **delta em tempo quase real**; a varredura
continua como conferência/rede de segurança.

## Arquitetura

- **Extensão MV3** (`SMSMais.chrome`): captura o **envio** (via `webRequest` + hook de fetch/XHR) e o
  **retorno** (HTML da tela, lido do frame). Um blur exige login no SMSMarica; envia lotes
  comprimidos para `POST /extensao/sisreg/capturas`. Detalhes no README do projeto.
- **Só observa.** Nenhuma requisição é disparada ao SISREG.
- **Persistência (fase de análise):** `smsmarica.sisreg_captura_navegador` — uma linha por evento
  (envio, resposta ou ajax), com o payload cru. Descartável depois.
- **Processamento (fase 1):** um processador no **servidor** lê as capturas, correlaciona
  envio+resposta e cria/atualiza a `Solicitacao` reusando o pipeline do import do TXT. A lógica
  frágil (ler a tela do SISREG) fica no servidor — conserto por deploy, sem recarregar os PCs.
- **Monitores:** `GET /extensao/sisreg/capturas/resumo` (contagens por PC/kind/evento/caminho/etapa,
  sem PII) e `.../estrutura` (nomes de campo do envio + rótulos da resposta, sem PII).

## O que já observamos em produção (15/09/2026)

Um PC de uma **médica reguladora** gerou ~3.000 capturas num dia. As etapas reais:

| Caminho · etapa | O que é | Vezes |
|---|---|---|
| `marcar · MARCAR` | **gravar a marcação** (agendar o paciente na vaga) | ~37 |
| `marcar · LST_VAGAS` / `LST_ITENS_PA` / (vazio) | passos anteriores da marcação | ~110 |
| `cadweb50 · DETALHAR` | buscar o paciente (CADSUS) | ~40 |
| `gerenciador_solicitacao · LISTAR/VISUALIZAR` | consultar solicitações | ~28 |
| `cons_verificar · EXCLUIR_SOLICITACAO` | **cancelar** | 1 |
| `autorizador` (sem etapa de escrita) | fila do regulador (só navegação capturada) | ~1.778 |

## Fase 1 — ações tratadas agora

### Marcação — `marcar · MARCAR`

**É agendar o paciente numa vaga existente** (consome a vaga; o SISREG gera o `co_solicitacao` e a
Chave de Confirmação). Não é "regular".

- **Campos do envio** (já estruturados): `cns`, `pa` (procedimento SISREG), `cid10`, `cpfprofsol`/
  `nomeprofsol` (solicitante), `upsexec`, e a vaga: `vaga_cpfprofexec` (CPF do executante),
  `vaga_dataexec` + `vaga_horaexec` (data/hora), `vaga_cod_escala`, `vaga_upsexec`.
- **Da resposta:** `co_solicitacao` (número — **chave de idempotência**) e a Chave de Confirmação.
- **Como entra na base:** monta um `MarcacaoSisreg` e chama `ImportarMarcacoesAsync` — o **mesmo**
  ponto de entrada do import do TXT. Reusa dedup de paciente (CNS→base→CADSUS), auto-criação de
  unidade por CNES, **idempotência por número** e a **reconciliação** com a manual/TXT.
- **Vaga consumida = DERIVAÇÃO.** Não existe "baixar vaga": a ocupação é calculada cruzando
  `Solicitacoes × SisregEscala` (unidade + CPF do executante + procedimento + dia). Criar a
  solicitação já a faz contar nas Agendas — dinâmico, automático.

### Cancelamento — `cons_verificar · EXCLUIR_SOLICITACAO`

- **Campos do envio** (100% local, sem ler HTML): `codigo_solicitacao`, `justificativa`.
- **Como entra na base:** acha a `Solicitacao` pelo `CodigoSolicitacao` e seta `CanceladoEm` +
  `MotivoCancelamento`. Como a ocupação é derivada com `CanceladoEm IS NULL`, **cancelar libera a
  vaga automaticamente** nas Agendas.

## Proveniência e "somar" os RAWs

- **Marca de origem:** `Solicitacao.FonteCriacao` (`Manual` / `ImportacaoTxt` / `ExtensaoNavegador`)
  — para rastrear de onde a solicitação (ou a alteração) surgiu, se a base divergir.
- **Reconciliação:** o pipeline **complementa por número preservando a autoria** (trilha
  `ComplementadaImportacaoSisreg`), **não sobrepõe cego**. A extensão escreve em tempo real; quando
  o TXT chega com o mesmo número, ele confere/completa. Divergências viram trilha, não sobrescrita
  silenciosa.

## Segurança / LGPD (decisões desta fase)

- **Transporte:** HTTPS/TLS já cifra o corpo na rede. **Sem cifra extra no trânsito** — cifrar com o
  token do usuário na mesma requisição não agrega (o token viaja junto) e o token não é uma chave.
- **Em repouso:** o raw é guardado **como está** nesta fase de análise. A **Chave de Confirmação** é
  dado sensível (prova de comparecimento) — guardada em campo próprio, fora do raw renderizado.
- **Retenção:** o acervo cru é descartável após a análise.

## O que ainda NÃO tratamos — expectativa (roadmap)

Conforme outras requisições forem reveladas no tráfego real, tratar:

| Ação no SISREG | Efeito na base esperado | Situação |
|---|---|---|
| **Regular / autorizar** (`autorizador`, aplicar) | muda **status** da solicitação (autorizada/agendada) | maior volume observado, mas **sem etapa de escrita identificável ainda** — provável AJAX; precisa melhorar a captura |
| **Criar / excluir agenda** (escala do executante, `cons_escalas`) | cria/remove **vagas** (aí sim direto, não só por derivação) | **não observado** — esta médica não tem acesso de executante; instrumentar um PC executante |
| **Criar solicitação** / **devolver à regulação** (`REENVIAR_REGULACAO`) | nova solicitação / volta à fila | não observado ainda |
| **Confirmar / Falta** (`cons_agendas`) | comparecimento / falta | não observado (ninguém tocou hoje) |

A expectativa é que **cada uma vire um evento** que atualiza a base pelo mesmo princípio: capturar
(envio + retorno), correlacionar no servidor, e aplicar o delta reusando o pipeline existente —
sempre com a varredura diária como conferência.

## Endpoints

| Método | Rota | Papel |
|---|---|---|
| POST | `/extensao/sisreg/capturas` | ingest dos lotes (autenticado; qualquer usuário do SMSMarica) |
| GET | `/extensao/sisreg/capturas/resumo` | monitor: contagens/metadados (sem PII) |
| GET | `/extensao/sisreg/capturas/estrutura` | nomes de campo do envio + rótulos da resposta (sem PII) |
