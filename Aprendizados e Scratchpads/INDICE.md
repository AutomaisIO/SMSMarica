# ÍNDICE GERAL — ferramentas, laboratórios e onde já se aprendeu o quê

> **Para que este arquivo existe:** parar o retrabalho de reabrir cinco pastas para descobrir se
> já existe um script que faz o que você precisa. Em 08/09/2026 havia **125 scripts** espalhados
> por quatro laboratórios, quase todos com um docstring bom explicando o que fazem — e nenhum
> lugar que dissesse que eles existem.
>
> **Regra de manutenção:** criou script novo num laboratório? Acrescente uma linha aqui.
> Para saber o que falta, rode:
>
> ```bash
> python "Aprendizados e Scratchpads/ferramentas/catalogar.py" --faltando
> ```
>
> Ele lista todo script dos laboratórios que este arquivo não menciona — e marca o que está
> **fora do git**. Índice que ninguém consegue conferir envelhece em uma semana.

---

## 1. "Preciso fazer X" → vá direto para

| o que você quer | onde já existe |
|---|---|
| **Olhar o banco de produção** (SELECT) | `Aprendizados e Scratchpads/ferramentas/db.py` — `from db import q` |
| **Ver o estado da varredura SISREG** num comando | `Aprendizados e Scratchpads/ferramentas/retrato.py` |
| **Acompanhar uma varredura viva** até o veredito | `Aprendizados e Scratchpads/ferramentas/acompanhar.py` |
| **Saber se o SISREG entrega uma faixa de datas** | `Automais.SISREG/sonda_dias_faltantes.py` |
| **Testar login / sessão** num sistema externo | `login_test.py` (SISREG), `probe_login.py` (SERNIT) |
| **Descobrir uma tela nova** de sistema externo | os `probe_*.py` / `recon_*.py` / `sonda_*.py` do laboratório correspondente |
| **Consultar cadastro por CNS/CPF** | `Automais.SISREG/consultar_cns.py`, `Automais.SER/resolver_identidades.py` |
| **Rodar SQL no Oracle do Salux** (read-only) | `Salux/scripts/conexao.py` + `_guard.py` |
| **Capturar as queries que o Salux desktop faz** | `Salux/scripts/marcar.py` → navegar → `capturar.py` → `analisar.py` |
| **Rodar a suíte sem brigar com outra sessão** | `dotnet test -p:BaseOutputPath=$TEMP/<pasta>/` |

---

## 1b. Acesso a servidor, banco, MikroTik — **isto já é skill, não redescubra**

Antes de investigar "como conecto em X", chame a skill. Ela existe justamente para você não
refazer o caminho:

| preciso de | skill |
|---|---|
| Rodar SQL no Postgres de produção **a partir do servidor** | `acessar-banco-no-servidor` |
| Mexer no servidor de produção: systemd, logs, nginx, deploy, migration, porta, env | `operar-servidor` |
| Configurar o MikroTik de uma unidade (bridge, VPN VOIP, failover, blindagem, automais.io) | `configurar-mikrotik-unidade` |
| Sincronizar o repo antes de editar | `sincronizar-antes-de-editar` |
| Abrir / resolver / fechar ticket do Suporte | `criar-ticket`, `resolver-ticket`, `fechar-ticket` |
| Marcar um `ERRO-XXXXXX` como resolvido | `resolver-erro` |
| Analisar o robô de atendimento sobre tráfego real | `analisar-robo-cenarios-reais` |
| Tratar um indicador contratual do HMCML | `tratar-indicador-hmcml` |

**Consultar o banco do seu próprio micro** (sem SSH, só SELECT) é outra coisa e não tem skill:
use `ferramentas/db.py` daqui.

**Descobriu algo que a skill não dizia?** Atualize a skill — não deixe o aprendizado só na
conversa, que morre com a sessão. É a mesma regra do índice.

---

## 2. Regras que valem para TODOS os laboratórios

1. **Produção real.** Os laboratórios falam com sistemas de verdade (SISREG, SER, SERNIT, Oracle do
   HCML). Só leitura por padrão; escrita exige OK explícito.
2. **`.env` e `capturas/` são gitignored em todos** — contêm credencial e resposta com PII.
3. **Saída com dado de paciente nunca entra no repositório.** As sondas mandam gravar em `--saida`
   apontando para fora da árvore. Um TXT de agenda do SISREG tem nome, CNS, telefone e endereço.
4. **Orçamento anti-robô é do OPERADOR, não da unidade** (SISREG): ~700 requisições/hora somando
   varredura, mapeamento e CADSUS. Estourar = CAPTCHA = unidade pausada 24h e um humano no
   navegador. **Nunca depurar clicando de novo** — instrumente e capture na primeira tentativa.
5. **`sys.path` faz os scripts dependerem de onde estão.** 27 deles resolvem import por
   `__file__.parent` e 16 acham `.env`/`capturas/` a partir dele. **Mover um script para subpasta
   quebra os três de uma vez, em silêncio.** Foi por isso que a organização aqui é por ÍNDICE, e
   não por mudança de pasta.

---

## 3. `Automais.SISREG` — 23 scripts

Motor em `sisreg/client.py` (`SisregClient`: priming + login + sessão). Aprendizados vivos em
`docs/APRENDIZADOS.md`.

**Leitura / extração**
| script | o que faz |
|---|---|
| `consultar_agendas.py` | Agenda de Profissional (`cons_agendas`) — somente leitura |
| `consultar_marcados_reg.py` | "Agendados pela Regulação" (`cons_marcados_reg`) |
| `extrair_marcados.py` | extrator completo do `cons_marcados_reg` |
| `extrair_agenda_unidade.py` | materializa TODA a agenda de uma unidade executante |
| `exportar_escalas.py` | grade de escalas ambulatoriais (`cons_escalas`) |
| `consultar_cns.py` | consulta por CNS (`cadweb50`) — espelha o que o backend .NET faz |

**Implantação / carga**
| script | o que faz |
|---|---|
| `colher_rede.py` | colhe a REDE INTEIRA (todas as unidades, futuro e passado) |
| `colher_implantacao.py` | baixa o TXT bruto do `expo_solicitacoes`, unidade por unidade |
| `importar_solicitacoes.py` | importa o histórico (994 mil) para `smsmarica.solicitacao` |
| `conciliar_pacientes.py` | liga pacientes do SISREG ao hub FHIR, ou cria os que faltam |
| `relatorio_implantacao.py` | o que entrou, o que não entrou e por quê |
| `backfill_datas.py` | preenche `data_agendada` pelo `cons_agendas` |

**Escrita no SISREG e conciliação de cancelamentos (20/09/2026)**
| script | o que faz |
|---|---|
| `cancelar_solicitacao.py` | **ESCREVE no SISREG**: cancela uma solicitação da fila de Cancelamento do SMSMais pelo `cons_verificar` (etapa `EXCLUIR_SOLICITACAO`). Lê a situação na ficha ANTES (se já cancelada, não envia), localiza o checkbox exato na listagem (nunca adivinha `chk_N`), e prova o resultado relendo a ficha — nunca pelo alerta nem pelo HTTP 200. Sem `--executar` só mostra. Validado em produção em 20/09 |
| `consultar_canceladas.py` | cancelamentos da rede por período (`cons_marcacao_cancelada`, `tp_periodo=C`, máx. 31 dias; 20 por página; o rodapé ANUNCIA o total (`MARCAÇÕES PESQUISADAS (N)` + `de P` páginas) e o coletor exige que o lido bata com o declarado — ignorar isso perdeu 269 linhas na primeira versão). Traz justificativa, operador e instante do cancelamento. `--conciliar` cruza com a nossa base — em 31 dias, 503 códigos cancelados no SISREG continuavam de pé no SMSMais |

**Sondas — cada uma responde UMA pergunta**
| script | pergunta que respondeu |
|---|---|
| `sonda_export_amplo.py` | o `expo_solicitacoes` aceita recorte mais amplo que um par? **Sim** — `cpf=0`+`procedimento=0` traz a unidade inteira em 1 requisição (272 → 1) |
| `sonda_dias_faltantes.py` | uma faixa de datas existe mesmo, ou o SISREG erra? Achou erro **determinístico**: `alert(...)` com HTTP 200 e 504 |
| `sonda_fila_gerenciador.py` | dá para tirar a fila de espera do `gerenciador_solicitacao`? |
| `recon_escalas.py`, `recon_fila_espera.py`, `recon_menu.py` | reconhecimento de tela |
| `teste_consagendas_dia.py`, `teste_export_cmi.py` | validação pontual (não commitados) |

**Diagnóstico:** `login_test.py`, `debug_login.py`, `explore.py`

---

## 4. `Automais.SER` — 31 scripts

Motor em `ser/`. É onde está a maior massa de **sondas de tela JSF/RichFaces**.

**Identidade e divergências** (a frente que achou paciente trocado)
| script | o que faz |
|---|---|
| `resolver_identidades.py` | resolve em massa CPF → CNS + cadastro, em paralelo |
| `coletar_divergencias.py` | junta todas as divergências e as solicitações que dependem delas |
| `classifica_divergencias.py` | separa **a mesma pessoa** de **pessoa trocada** |
| `dossie_divergencias.py`, `relatorio_divergencias.py`, `relatorio_divergencias_operacional.py` (com PII), `render_divergencias.py` | relatórios |

**Sondas de criação/edição de solicitação:** `probe_tela_criar.py`, `probe_criar_solicitacao.py`
(escrita real), `probe_editar_solicitacao.py`, `probe_campos_dinamicos.py`, `probe_radio_dinamico.py`,
`probe_radios_bloco_fixo.py`, `probe_pesquisa_paciente.py`, `probe_reabrir_solicitacao.py`,
`probe_followup.py`

**Sondas de CID:** `probe_cid.py`, `probe_cid_catalogo.py`, `probe_cid_grupos.py`,
`probe_cid_mastologia.py`

**Sondas de módulo/sessão:** `sonda_modulo.py`, `sonda_modulo_home.py`, `sonda_modulo_novo.py`,
`sonda_tela_direta.py`, `sonda_paralelo.py` (**provou que N sessões simultâneas com a mesma
credencial funcionam** — é a base da pré-carga paralela)

**Outras:** `probe_historico.py`, `probe_historico_por_id.py`, `probe_solicitante.py`,
`probe_export_solicitacao.py`, `probe_ambulatorio_estadual.py`, `probe_sisreg_detalhe.py`

---

## 5. `Automais.SERNIT` — 18 scripts, **nenhum commitado**

Laboratório em recon. As sondas são **numeradas na ordem em que foram feitas** — leia nessa ordem
para entender o sistema:

`probe_login.py` (0) → `probe_pesquisa.py` (1) → `probe_grade.py` (2) → `probe_historico.py` (3) →
`probe_menu_acao.py` (4) → `probe_fatiar.py` (5) → `probe_editar.py` (6) →
`probe_followup.py` (7, **mapa apenas, nunca envia**) → `probe_nova.py` (8) →
`probe_campos_dinamicos.py` (9) → `probe_cid.py` (10)

**Diagnóstico:** `probe_diag_editar.py`, `probe_diag_xrw.py` (o `X-Requested-With` num submit
não-ajax faz o A4J devolver a tela errada), `probe_busca_id.py`, `probe_carry.py`, `probe_fix.py`,
`probe_poluicao.py`, `probe_busca_id2.py` (sem docstring — segunda tentativa da busca por id)

⚠️ **Está tudo fora do git.** Se essa máquina se perder, o laboratório inteiro se perde.

---

## 5b. `Automais.klinikos` — 10 scripts, **nenhum commitado** (criado 16/09/2026)

Laboratório de recon do **Klinikos** (HIS da Eco Sistemas: Conde, UPA Maricá e Santa Rita) pelo
caminho "como usuário" — login web + relatórios + `.asmx` — para **substituir a leitura direta do
SQL Server** (`KlinikosImportacaoStrategy` e `ConsultasUpa` do Painel do Secretário). Método do
SER/SERNIT; alvo e perguntas próprios. Tudo medido contra `klinikosconde.smsmarica.online`.

**Leia primeiro:** `docs/APRENDIZADOS.md` (stack, login com sessão única, gate do local, o que
precisa de ViewState — só o login — e o que não precisa), `docs/inventario-consultas-atuais.md`
(cada consulta SQL de hoje × fonte web candidata + política de requisições + plano de corte),
`docs/sondagem-relatorios.md` (parâmetros `parN` e colunas do XLS de cada relatório sondado),
`docs/catalogo-relatorios.md` (268 relatórios/271 telas dos menus), `docs/mapa-endpoints.md`
(368 telas rastreadas: campos, botões, endpoints).

`klinikos/client.py` (sessão, login + confirmação, gate, cookies persistidos, trava de leitura,
`postback()`) → `probe_login.py` (0) → `probe_tela.py <url>` (1, descreve uma tela) →
`probe_mapa.py` (2, rastreador GET de todas as telas) → `catalogo_relatorios.py` (3, menus →
CSV) → `probe_relatorio.py <parrel>` (4, replica a tela de parâmetros e captura o `window.open`)
→ `probe_rptview.py "<query>"` (5, GET direto do `rptview`/`rptviewXls`, descreve PDF/XLS) →
`sondar_relatorios.py <parrel|url>…` (6, lote: campos + `parN` + colunas do XLS)

**Utilitários:** `decodificar_viewstate.py` (strings/URLs de um `__VIEWSTATE`), `analisar_har.py`
(agrupa um HAR do DevTools por endpoint), `gerar_mapa_md.py` e `gerar_sondagem_md.py` (capturas → `docs/*.md` sem PII).

**Regras locais:** `export MSYS_NO_PATHCONV=1` antes de passar `/KlinikosNet/...` como argumento
no Git Bash; **uma sonda por vez** (o ASP.NET serializa a sessão e o Crystal dá 504 na fila);
relatório sempre com **1 dia** (30 dias estoura o Crystal); `capturas/` tem PII e é gitignored;
o usuário do laboratório derruba a sessão web de quem estiver logado com ele.

---

## 5c. `Automais.prime` — 19 scripts, **nenhum commitado** (criado 16/09/2026)

Recon do **Prime Saúde** (Eco Sistemas, `marica.ecosistemas.com.br/Prime`, hospedado pela Eco).
**Já respondido:** apesar da raiz `AtencaoBasica/`, o Prime em uso é o da **atenção
especializada**. CDT, CMI, Reabilitação, Ambulatório Péricles, SAE, CEOs e Melhor em Casa somam
4.658 atendimentos (~2.600 pacientes por CNS) por semana. As 25 USFs têm **zero** de 2018 a hoje. A especializada entrou
em 2025. Não há conector do SMSMais lendo o Prime.

**Leia primeiro:** `docs/APRENDIZADOS.md` (login `formLogin`, gate de unidade sem `ddlPerfil`,
relatório = GET `*RPT.aspx?...&extensao=CSV&unidades=<GUID>`, 31 `.asmx`) e
`docs/mapa-endpoints.md` (123 telas).

`prime/client.py` → `probe_login.py` → `probe_tela.py <url>` → `probe_mapa.py` (+
`gerar_mapa_md.py`) → `sondar_atividade.py DD/MM/AAAA DD/MM/AAAA [guid…]` (atendidos por
unidade, só contagens). `probe_atendidos.py` = tentativa por postback que revelou o `window.open`.

**Escrita por API (§15–17):** `probe_cadastro_direto.py` cadastra paciente em **1 POST** —
`prime/combos.py` resolve os códigos internos pelos `.asmx` e `prime/tipos_logradouro.py` faz o
de-para CadWeb→Prime. Os postbacks **não** são necessários quando os códigos já estão resolvidos.

**Ingestão do clínico (§19–20):** `prime/relatorio_csv.py` remonta o CSV **malformado** do Prime
(sem aspas, com LF dentro dos campos — `DictReader` desalinha em silêncio) e `prime/atendidos.py`
normaliza em atendimento. `backfill_atendidos.py INI FIM [--seco]` varre dia × unidade para
NDJSON; `conferir_noite.py [dia]` cruza relatório × capturas da extensão e sai com código ≠ 0 se
houver alerta. **O horário de término é o `DataRegistro`** — `DataInicio`/`DataFim` trazem
`00:00:00` em 100%. 1.093 atendimentos/dia em 8 unidades; USF zerada é normal (é Klinikos).

**Hub FHIR (§21):** `prime/cns.py` valida o DV do CNS e `prime/unidades.py` faz o de-para
GUID→CNES; `preparar_hub.py` monta Encounter+Condition em arquivo **sem gravar nada**;
`criar_unidades_faltantes.py` criou CEREST e Odontomóvel no `smsmarica.unidade` (51→53, 23/09/2026)
com CNES do cadastro nacional — **`0209724` precisa do zero à esquerda**, a API do CNES serializa
como inteiro e todas as nossas são de 7 dígitos. Achados que
mudam decisão: **96,2% dos CNS do Prime são provisórios — e o nosso hub é 99,3%**, então a régua é
DV válido, não série (exigir definitivo importaria 362 de 243.330); casar por `cns_todos` sobe de
72,8% para 93,5% (~7.900 duplicatas evitadas); e **`procedure`/`service_request` não existem no
schema `fhir`**, então o SIGTAP (100% preenchido) não tem onde pousar sem criar o recurso.

**Regras locais:** sessão única (a primeira sonda derrubou a sessão aberta da conta); uma sonda
por vez; `capturas/` tem PII e é gitignored. CSV de atendidos: registro = CRLF (LF solto dentro;
contar linhas infla ~3×). "Consultar Cadweb" existe e foi **descartado**; e-SUS no Prime só exporta
fichas. Estado e abertos: `docs/APRENDIZADOS.md` §8.

## 5d. `Automais.saudemental` — 6 scripts, **nenhum commitado** (criado 16/09/2026)

**Prime Saúde Mental** (`/SaudeMental`, build `2025.08.0.18`, marca inclui TEA): outro produto no
mesmo host da Eco, com a mesma conta do Prime. **Já respondido:** é o sistema dos **3 CAPS**
(Gilberto desde 27/05/2025, AD desde 16/06/2025, Infanto-Juvenil desde 01/07/2025). Todos estão
ativos (568/166/174 atendimentos em 09–15/09/2026).

**Leia primeiro:** `docs/APRENDIZADOS.md`. Relatórios = uma tela com 47 ImageButtons → POST cria
instância do **Telerik Reporting 7.2** → GET `Telerik.ReportViewer.axd?instanceID=…&optype=Export&ExportFormat=CSV`.
**Armadilha:** a data só vale com o `…_dateInput_ClientState` JSON. Sem ele, vem o histórico
inteiro (30 MB/31 s) sem erro. **CPF/CNS:** relatório `imbDocumentacaoUsuarios` por CAPS (96–100% com documento, DV válido); atendimentos ligam por nome (210/211).

`saudemental/client.py` → `probe_login.py` → `probe_tela.py` → `probe_mapa.py` (+
`gerar_mapa_md.py`) → `probe_relatorio.py <imb> <rdpIni> <rdpFim> ini fim [CSV|PDF]` →
`sondar_atividade.py ini fim` (atendimentos por CAPS, troca a unidade pelo gate), `sondar_desfechos.py ini fim [guid]` (perfil sem PII dos desfechos). **Prime e Saúde Mental dividem a sessão única da conta: nunca alternar os dois labs.** **Use XLS** (o CSV do
Telerik omite o detalhe). Estado e abertos: `docs/APRENDIZADOS.md` §8.

---

## 6. `Automais.SISCAN` — 8 ferramentas na raiz

⚠️ **Os `.py` e os `.md` do lab foram perdidos** (sobrou o `__pycache__`; as capturas e os
`docs/*.html` continuam lá). `siscan/client.py` e `siscan/exame.py` foram **reconstruídos** em
22/09/2026 a partir do bytecode + `docs/APRENDIZADOS.html` e reconferidos contra o SISCAN real.
Continuam perdidos: `mapear_formulario.py`, `probe_modais.py`, `probe_variacoes.py`,
`probe_requisicao.py`, `probe_menu.py`, `teste_edicao_requisicao.py`,
`teste_troca_responsavel.py`, e os `.md` de `docs/`.

**Pacote:** `siscan/client.py` (login SHA-256, menu, `post_form`, `post_a4j`, `aplicar_a4j`,
trava de escrita com allowlist), `siscan/exame.py` (GERENCIAR EXAME: filtro, grade, ações),
`siscan/requisicao.py` (**criar requisição**: Novo Exame → CNS → tipo → unidade → Avançar →
tipo de mamografia → Responsável), `siscan/inspecao.py` (relatório legível de uma tela).

**Sondas e ferramentas:**
`probe_nova_requisicao.py --cns <CNS> [--unidade N --mamografia 01|02 --responsavel N]` — vai até
**um clique antes do Salvar** e para · `criar_requisicao.py` (só grava com `--confirmar`; os casos
vivem em `casos.py`, gitignored por ter PII) · `conferir_requisicao.py` · `probe_duplicidade.py
--cns` (a paciente já tem requisição?) · `probe_sessao_unica.py` (**medido: o SISCAN aceita duas
sessões simultâneas**) · `mapear_requisicao_nova.py`.

**Backfill das requisições lançadas à mão** (23/09/2026, tudo somente leitura menos o último):
`espelho_requisicoes.py --de --ate --saida` varre GERENCIAR EXAME e devolve **todas** as linhas —
a grade tem coluna **Cartão SUS**, e é isso que permite cruzar com as nossas anamneses sem abrir
requisição nenhuma · `probe_leitura_lote.py` mediu o ciclo de leitura em massa ·
`backfill_siscan.py --espelho --saida` cruza com o banco (CNS + mesmo dia + única dos dois lados),
abre cada requisição e monta o relatório · `aplicar_backfill.py --relatorio --desfazer
[--confirmar]` é o **único que escreve** (em produção, e gera o desfazer antes).

**Três medidas que fazem a varredura ser barata:** `frm:tamanhoPagina` aceita **300** por página
(padrão 10); o rodapé traz o total real, então **partir a janela de datas ao meio** substitui
paginar (o datascroller RichFaces exigiria engenharia reversa); e o `frm:botaoVoltar` da tela da
requisição **devolve a grade com os resultados** — abrir custa 0,2 s e voltar 0,2 s, contra os
14–32 s de um clique de menu. Ler 551 requisições leva minutos, não horas.
**`frm:prontuario` vazio = requisição digitada à mão**; com o nosso accession = saiu daqui.

O fluxo, as armadilhas e o que ficou em aberto estão em `docs/FLUXO-NOVA-REQUISICAO.md`, e o plano
de gerar a requisição a partir da nossa anamnese em `docs/PLANO-REQUISICAO-PELA-ANAMNESE.md`.
Estado geral: `docs/CONTINUACAO.html`.

**O que o CNS resolve:** o `onblur` de `frm:cartaoSUS` traz do CADSUS nome, nascimento, mãe,
raça/cor e endereço (todos disabled) **e o histórico de exames do paciente** — caminho barato
de leitura por CNS. **Ordem imposta:** tipo de exame popula Unidade Requisitante; tipo de
mamografia popula Responsável (e a lista muda entre diagnóstica e rastreamento — o `value` é
posicional, **resolver pelo CNS do profissional**).

---

## 7. `Salux/scripts` — 53 scripts

⚠️ **`Salux/CLAUDE.md` tem regras próprias e não negociáveis.** O Oracle de PRODUÇÃO é
**read-only absoluto**, garantido por `_guard.py`, que rejeita qualquer SQL que não seja
SELECT/WITH/EXPLAIN.

**Infra:** `conexao.py`, `_guard.py`, `teste_conexao.py`, `ssh_servidor.py`, `probe_servidor.py`,
`setup_observador.py`, `diagnostico_privilegios.py`

**Captura do desktop (o método):** `marcar.py` / `marcar_retroativo.py` → o usuário navega →
`capturar.py` → `analisar.py` → gera `docs/queries/<tela>.md`. Apoio: `marca.py`, `monitor.py`,
`espiar_ultima.py`, `identificar_sessao.py`, `sessoes_amplas.py`, `snapshot_sql_ativo.py`,
`analisar_snapshot.py`, `buscar_query.py`

**Descoberta de schema** — o clínico do Salux mora em `INFOSAUDE` (BAA/FIA/EDOC por `CD_PACIENTE`):
`achar_prontuario.py`, `achar_prontuario2.py`, `achar_prontuario3.py` (mede o custo por paciente),
`achar_edoc.py`, `achar_edoc2.py` (onde está o TEXTO da evolução), `achar_medico.py`,
`achar_especialidade.py`, `achar_conselho.py`, `achar_conselho2.py` (CRM × COREN × CRN),
`achar_lookups.py`, `achar_tabela.py` (em qual owner está), `listar_tabelas.py`,
`listar_modelos_edoc.py`, `inspecionar_tabela.py`, `colunas_paciente.py`, `mapear_fks.py`,
`fks_individuais.py`, `estatisticas_banco.py`, `dump_edoc_movimento.py`, `dump_lookup.py`,
`descobrir_paciente_fia.py`, `buscar_paciente.py`, `inspecionar_triggers_auditoria.py`,
`investigar_pendencias.py`, `investigar_sincronizacao.py`, `gerar_html.py`

⚠️ `_limpar_para_refator_fhir.py` **apaga** laudos/tratamentos/solicitações no Postgres — é
dev-only e não tem nada a ver com o Oracle. Nome parecido, consequência oposta: leia antes.

**Engenharia reversa do PowerBuilder:** `extrair_sql_de_pbd.py`, `catalogo_sql_pbd.py`

**Importação para o hub FHIR:** `importar_10_fhir.py`, `importar_medicos_fhir.py`,
`importar_atendimentos_fhir.py`, `verificar_prescricao_baa.py`

**Inventário/estoque:** `exportar_inventario_completo.py` (não commitado)

---

## 8. Onde ficam as outras memórias do projeto

| lugar | o que guarda |
|---|---|
| `docs/` + `docs/adr/` | decisões arquiteturais — a fonte canônica |
| `alterações entre sessoes/farol-*.md` | coordenação entre sessões paralelas (gitignored) |
| `Aprendizados e Scratchpads/` | **este índice** + `ferramentas/` versionadas + `scratchpad/` (ignorado) |
| `alterações entre sessoes/ferramentas/LEIA-ME.md` | a régua de PII: o que pode e o que não pode sair do scratchpad |
| `Automais.*/docs/APRENDIZADOS.md` | o que se descobriu de cada sistema externo |
| `Salux/CLAUDE.md` | regras próprias da pasta Salux |
| `SMSMais.Regulacao/PROGRESSO.md` | estado da frente de Regulação |

---

## 9. O que eu NÃO fiz, e por quê

**Não movi nenhum script para subpasta.** Medido: 27 scripts fazem
`sys.path.insert(0, str(pathlib.Path(__file__).parent))` e 16 montam `BASE = __file__.parent` para
achar `.env` (6) e `capturas/` (17). Mover para uma subpasta faria os três falharem de uma vez — e
pior, em silêncio, porque `.env` ausente vira "credencial não encontrada" e `capturas/` seria
recriado no lugar errado.

Se a reorganização física for mesmo desejada, o caminho honesto é: mover um laboratório por vez,
trocar `__file__.parent` por `__file__.parent.parent`, e **rodar cada script** para confirmar. São
125 scripts contra sistemas externos com orçamento anti-robô — o ganho não paga o risco enquanto um
índice resolve a dor real, que é **saber que a ferramenta existe**.
