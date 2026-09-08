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

## 6. `Automais.SISCAN` — sem scripts na raiz

Só `siscan/`, `docs/` e `capturas/`. O estado está em `docs/CONTINUACAO.md`.

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
