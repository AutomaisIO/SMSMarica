# 13 — Spikes de laboratório (incremento 0)

## Objetivo

Responder, **antes de escrever código de produto**, às perguntas que só o sistema real responde. Cada spike tem ferramenta, critério de saída, custo em requisições e regra de autorização. O resultado de cada um é um relatório em `revisoes/` que os planos dependentes leem.

## Requisitos cobertos

R-04, R-05, R-06, R-12, R-15, R-17 — indiretamente, todos que dependem do comportamento real dos sistemas.

## Decisões aplicadas

D-4 (SISREG só depois do spike b), D-8, D-9.

## Regras que valem para todos

- **Somente leitura não precisa de OK; escrita real precisa de OK explícito do Bernardo**, registrado em `PROGRESSO.md` antes de rodar.
- **Credencial**: nunca a de sincronismo institucional. SER/SERNIT: credencial pessoal do operador que autorizou. SISREG: credencial **dedicada** ao spike (sessão única por operador — usar a do robô derruba a varredura noturna; usar a de um humano derruba o navegador dele).
- **Uma tentativa, tudo capturado**: HTML cru de cada etapa vai para `capturas/` (gitignored, PII). Se falhar, analisar a captura antes de repetir. No SISREG cada clique é orçamento (~700 → CAPTCHA por 24h).
- Relatório em `SMSMais.Regulacao/revisoes/spike-<letra>-<assunto>.md` com: data, credencial usada (login mascarado), requisições gastas, respostas às perguntas, o que mudou nos planos.

## Spike a — SER: Gravar solicitação + anexo (escrita real, exige OK)

**Pergunta:** o robô consegue criar uma solicitação no SER, subir um anexo antes do Gravar, capturar o número e reverter?

**Ferramenta:** `Automais.SER/probe_criar_solicitacao.py` já tem modo ensaio e flag `--gravar` nunca acionada. Estender com o upload do `formAnexar` conforme `docs/ser-criar-solicitacao.md §2.3` (form separado, `enctype=multipart/form-data`, `rich:fileUpload`, um POST por arquivo para `solicitar-consulta-editar.seam?_richfaces_upload_uid=…&AJAXREQUEST=_viewRoot`, `maxFileBatchSize: 2`, `noDuplicate: true`). Menu de ações por situação: `probe_reabrir_solicitacao.py`.

**Preparo:** paciente de teste combinado com a SES-RJ (ou um paciente real com pedido legítimo a ser feito de qualquer forma). Recurso simples, sem CID restrito. Fora do pico.

**Critério de saída (todos):**
1. Gravar devolve número; a releitura do histórico da solicitação confirma o evento "Solicitar" (padrão de `SerEscritaService`: "Registrado!" não é prova).
2. O anexo aparece em `form0:anexoList` da solicitação criada.
3. Reversão: o solicitante consegue **Cancelar** na situação recém-criada (ler o menu Ação). Se não conseguir, o cancelamento é combinado com a central **antes** de gravar.
4. Capturados: ids voláteis do Gravar e do upload, resposta do Gravar (redirect com `cid`?), formato do número.

**Custo:** 15–25 requisições; o SER não tem CAPTCHA nem orçamento conhecido.

**Alimenta:** plano 12 (tarefas 5.5–5.6), ADR-0054.

## Spike b — SISREG: tela `marcar` ponta a ponta (escrita real, exige OK)

**Perguntas:** (1) quais campos cada etapa pede por tipo de procedimento; (2) o perfil solicitante consegue gravar; (3) **existe janela editável** e por quantos dias; (4) como reverter (cancelar/excluir); (5) existe follow-up na tela do solicitante; (6) como o operador seleciona a unidade (CNES) — no login ou no `marcar`; (7) a credencial "padrão" do agente enxerga várias unidades; (8) o CAPTCHA é por operador ou por IP.

**Ferramenta:** novo `Automais.SISREG/probe_marcar.py`, sobre `sisreg/client.py`. Mapa por GET já existe em `Automais.SISREG/docs/APRENDIZADOS.md` (§ "Subsídio para escrita"): `cadweb50?url=/cgi-bin/marcar` → `marcar` (`pa`, `cid10`, `cpfprofsol`/`nomeprofsol`, `ret`, `upsexec`) → `ProximaEtapa()`: código terminado em `000` vai para `LST_ITENS_PA`, senão `LST_VAGAS`; gravação a partir da tela de vagas. Outras escritas mapeadas: `gerenciador_solicitacao` (`CANCELAR_SOLICITACAO`, `REENVIAR_REGULACAO`), `cons_verificar` (`EXCLUIR_SOLICITACAO`).

**Preparo:** credencial dedicada de solicitante numa unidade de teste (pedir à regulação). Três procedimentos: um grupo (`…000`), um item de consulta, um exame. Paciente de teste.

**Critério de saída:**
1. Mapa de campos por etapa, com o HTML de cada uma, para os três procedimentos.
2. Solicitação criada, número capturado, e depois **cancelada/excluída** com sucesso.
3. Resposta objetiva às perguntas 3–8 (com evidência: captura da tela).
4. Medição: quantas requisições custou a inclusão inteira.

**Custo:** ≤ 30 requisições do operador dedicado (~4% do limiar). O orçamento global do robô (`SisregOrcamentoRequisicoes`) não é tocado porque a sessão é outra.

**Alimenta:** plano 11 (tudo), plano 04 (o que o OK do agente faz), plano 07 (modelo da senha padrão × por unidade), README §9 questões 1, 2, 3, 6, 7.

## Spike c — Paridade SER × SERNIT (só banco local)

**Pergunta:** quais recursos existem nos dois, com que nome, e os formulários são iguais?

**Ferramenta:** SQL contra a base (skill `acessar-banco-no-servidor`, leitura). Tabelas: `ser_catalogo_recurso` (tipo, valor, rotulo, ambulatorio_estadual) × `sernit_catalogo_recurso` (tipo, valor, rotulo); depois `ser_catalogo_campo` × `sernit_catalogo_campo` por rótulo + tipo + obrigatório. Chave de comparação: `unaccent(upper(trim(rotulo)))` por `tipo` — o `valor` do combo difere entre as instâncias e não serve.

**Critério de saída:** relatório com quatro classes — só-SER, só-SERNIT, ambos-iguais, ambos-com-diferença (campo a campo) — e a decisão da **chave de pareamento** e da **régua união** por par.

**Custo:** zero externo.

**Alimenta:** planos 08 e 02 (tarefa 2.6).

## Spike d — Classificação de follow-up (só banco local)

**Pergunta:** os textos de follow-up do SER/SERNIT têm padrões suficientes para separar "falha de contato", "documento criticado", "agendamento" e "outro" sem IA?

**Ferramenta:** ~~amostra de 500 linhas … rotular 100 à mão~~ — **corrigido pelo spike d.** O corpus inteiro cabe (18.904 + 312), então não há por que amostrar para classificar. E **rotular 100 linhas aleatórias mede mal**: 40% cairiam na mesma classe. O que mede precisão é **amostra estratificada da classe predita** — ~130 itens lidos, ~12 por classe, com nova rodada a cada correção de regex.

**Critério de saída — atingido em 05/09/2026.** Relatório em [`revisoes/spike-d-followup.md`](revisoes/spike-d-followup.md); `regras_followup_json` inicial em [`revisoes/spike-d-regras-followup.json`](revisoes/spike-d-regras-followup.json); fixture de 60 casos em `SMSMais.server/tests/SMSMais.Tests/Regulacao/Pendencias/Fixtures/followups-rotulados.csv`.

**A taxonomia de 4 classes deste plano está errada** e virou 9. As duas maiores classes reais não estavam previstas: **SemVaga** (22% do SER) e **ReclassificacaoRisco** (9% do SER, **81% do SERNIT**). `Agendamento` é residual (0,7%) porque `Agendar` é evento próprio do SER. E o template que parecia "documento criticado" é **orientação ao paciente** — separar as duas é o que impede a fila de pendências do plano 06 de nascer com 5,5× itens falsos.

**Custo:** zero externo. **PII: anonimização automática não é confiável neste corpus** — deixa passar nome entre parênteses e destrói texto em CAIXA ALTA (chegou a transformar `PACIENTE AGENDADO PARA O HOSPITAL` em `PACIENTE <NOME> O HOSPITAL`). O fixture é montado só com textos que **se repetem ≥ 3 vezes** (repetição = template = sem nome), filtro contra telefone e dígitos longos, e conferência linha a linha.

**Alimenta:** plano 06 (tarefa 6.2) e plano 09 (`regras_followup_json`). Só **duas** das nove categorias viram pendência: `FalhaContato` → contato; `SolicitacaoAoSolicitante` → documento.

## Spike e — Extração dos manuais de elegibilidade (só arquivos locais)

**Pergunta:** quanto dos recursos do SER tem regra escrita nos manuais, e em que forma?

**Ferramenta:** ~~`pdftotext -layout`~~ — **corrigido pelo spike e: não serve.** A extração é **por geometria, com PyMuPDF**: as réguas horizontais desenhadas delimitam a linha da tabela e a régua vertical interna separa a coluna do recurso da de requisitos. Quatro defeitos que derrubam a abordagem por texto corrido estão documentados em [`revisoes/spike-e-manual-regras.md` §3](revisoes/spike-e-manual-regras.md) — o principal é que **o nome do recurso é `título da seção + célula`** (`4.1.5. ENDOCRINOLOGIA` + `DIABETES GESTACIONAL`), e ignorar o título derruba o pareamento de 50% para 9%.

O nome do arquivo do REUNI no repositório é `REUNI_MANUAL DO SOLICITANTE_V3 29.12.20222 - Copia.pdf` (com o typo no ano), não o que este plano citava.

**Critério de saída — atingido em 04–05/09/2026.** CSV `revisoes/spike-e-manual-regras.csv` com **três colunas a mais** do que o previsto (`manual`/`ramo_ser`, porque a mesma especialidade tem regra diferente em cada ramo do SER; `recurso_catalogo`/`pareamento`, para a importação não refazer o pareamento; `secao`, porque critério de **exclusão** importado como inclusão inverteria 295 regras).

**Custo:** zero externo. Sem OCR/IA: é extração de texto e digitação assistida.

**Resultado:** 204 recursos, **1.169 regras** — 83% NaoDedutivel, 14% Documental, 2% Dedutivel. Só **19% do catálogo SER** tem regra escrita (27% no ramo AE, 11% no outro); 50% dos recursos do manual não casam com o combo por divergência real de nome. Também confirmou, por outra via, que **CRECE = ramo "Sim" (Ambulatório Estadual) e REUNI = ramo "Não"**.

**Alimenta:** plano 03 (tarefas 4.1, 4.5) — e abre duas mudanças nele: falta um estado **"informativa"** (83% do corpus não pode virar pergunta obrigatória sem tornar o questionário impraticável) e a regra precisa de `secao` + `fonte`.

## Tarefas

- [x] c — **feito 04/09/2026** — `revisoes/spike-c-paridade.md`
- [x] d — **feito 05/09/2026** — `revisoes/spike-d-followup.md` + `revisoes/spike-d-regras-followup.json` + fixture de 60 casos
- [x] e — **feito 04–05/09/2026** — `revisoes/spike-e-manual-regras.csv` + `revisoes/spike-e-manual-regras.md`
- [ ] a — pedir OK, preparar paciente/recurso, estender a sonda, rodar uma vez, escrever `revisoes/spike-a-ser-gravar.md`
- [ ] b — pedir OK e credencial dedicada, escrever `probe_marcar.py`, rodar uma vez por procedimento, escrever `revisoes/spike-b-sisreg-marcar.md`, atualizar README §9

## Riscos

- Spike a: o SER pode recusar o Gravar por validação que só aparece no navegador (foi o bloqueio de 08/2026). Se acontecer, capturar a mensagem em `form0:messages` e parar; não insistir.
- Spike b: a credencial dedicada pode não ter permissão de gravar; isso já é resposta. CAPTCHA no meio do spike: parar, registrar em que requisição, e esperar 24h.
- Spike e: os manuais são de 2022; a regra pode estar defasada. O plano 03 prevê versão e fonte por regra justamente por isso.

## Fora de escopo

Qualquer código no server ou no front. Os spikes vivem nos labs Python e nos relatórios.

---

## Especificação para execução

### Template do relatório (`revisoes/spike-<letra>-<assunto>.md`)

```
# Spike <letra> — <assunto>
- Data: dd/mm/aaaa · Executor: <nome> · OK de produção: <sim/não, quem, quando> (só a e b)
- Credencial: <provedor>/<login mascarado> · Requisições gastas: N · CAPTCHA: não/sim (na requisição N)
## Perguntas e respostas
1. <pergunta> — <resposta objetiva>. Evidência: capturas/<arquivo>.
## O que muda nos planos
- Plano NN §X: <mudança> (registrar também em PROGRESSO.md → Desvios)
## Anexos (sem PII)
```

### Spike c — comandos

1. Abrir sessão no servidor conforme a skill `acessar-banco-no-servidor` (leitura).
2. Rodar as duas consultas do plano 08 §A (conferir antes os nomes das colunas de FK em `SerCatalogoConfiguration`/`SernitCatalogoConfiguration`).
3. Exportar para CSV no scratchpad; montar as quatro classes e a tabela de diferenças.
4. Escrever `revisoes/spike-c-paridade.md` com a decisão da régua união por par.

### Spike d — comandos

```sql
select e.observacao, e.usuario, e.lotacao_evento, s.recurso, s.situacao
from smsmarica.ser_evento e join smsmarica.ser_solicitacao s on s.id = e.ser_solicitacao_id
where upper(e.evento) like '%FOLLOW%' and e.observacao is not null
order by random() limit 500;
-- idem sernit_evento / sernit_solicitacao
```
Rotular 100 à mão numa planilha (`categoria` ∈ FalhaContato | DocumentoCriticado | Agendamento | Outro), escrever as regex, medir precisão por categoria em Python (`re`, `unidecode`), gravar `tests/SMSMais.Tests/Regulacao/Pendencias/Fixtures/followups-rotulados.csv` **sem nomes** (substituir nomes próprios por `<NOME>`), e o JSON inicial de `regras_followup_json` no relatório.

### Spike e — comandos

```bash
cd "Automais.SER/documentacao"
pdftotext -layout "CRECE_MANUAL DO SOLICITANTE_Versão1 30.11.2022.pdf" crece.txt
pdftotext -layout "REUNI_MANUAL DO SOLICITANTE_V3 29.12.2022.pdf" reuni.txt
```
Script Python no scratchpad: quebrar por título de recurso (linhas em caixa alta que casam com `ser_catalogo_recurso.rotulo` normalizado), capturar blocos "Critérios de inclusão" / "Critérios de exclusão" / "exame|documento|anexar|inserir", detectar idade (`\b(\d{1,2})\s*(anos|a)\b`, "a partir de", "acima de", "entre X e Y"), sexo ("mulher|homem|feminino|masculino"), e emitir o CSV `revisoes/spike-e-manual-regras.csv` (`recurso;sistema;tipo;texto_original;idade_min;idade_max;sexo;pergunta;documento;fonte`). Tudo o que não classificar vai para `tipo = NaoDedutivel` com `pergunta = texto_original` para revisão humana. Nota `.md` com cobertura (recursos com regra / total).

### Spike a — roteiro do `probe_criar_solicitacao.py --gravar --anexo`

1. `.env` do lab com credencial **pessoal** do operador que autorizou; paciente/recurso combinados; `--ensaio` primeiro (já existe) para conferir o formulário do dia.
2. Implementar `--anexo`: abrir a aba Editar; localizar `formAnexar` e o input `formAnexar:upload:file` (por estrutura, não por id fixo); POST multipart para `solicitar-consulta-editar.seam?_richfaces_upload_uid=<uuid>&AJAXREQUEST=_viewRoot` (ver §2.3 de `docs/ser-criar-solicitacao.md`); reler a tela e procurar o nome em `form0:anexoList`.
3. `--gravar`: preencher, acionar o botão Gravar (id lido do form), salvar a resposta; se vier `Ajax-Response: redirect`, seguir o `Location` e salvar a página de destino; extrair o número.
4. Reler o histórico da solicitação criada (`probe_historico`/leitor existente) e confirmar "Solicitar".
5. Ler o menu de ações (`probe_reabrir_solicitacao.py`) e, se houver **Cancelar**, cancelar; salvar; reler.
6. Testar anexo **depois** do Gravar pela aba Editar da solicitação criada (necessário para o documento criticado do plano 06): registrar se funciona.
7. Relatório `revisoes/spike-a-ser-gravar.md`.

### Spike b — roteiro

Está no plano 11 §B (`probe_marcar.py`). Regras: credencial dedicada; três procedimentos; uma execução por procedimento; reverter cada uma; contar requisições; parar no primeiro sinal de `recaptcha`.

### Ordem sugerida

~~c → e → d~~ **os três feitos (04–05/09/2026).** Restam **a** (antes do incremento 5) e **b** (antes do incremento 7), os dois com escrita real e **dependentes de OK explícito do Bernardo**.
