# ADR-0076 — Guia de todo agendamento regulado, numa fila por sistema, guardada uma vez no acervo do paciente

**Status:** aceito · **Data:** 2026-10-21
**Relacionado:** [ADR-0072](./0072-aviso-agendamentos-ser-sernit-esus.md) (`agendamento_externo`) ·
[ADR-0066](./0066-acervo-do-paciente-e-midia-do-whatsapp.md) (acervo do paciente) ·
[ADR-0042](./0042-ser-segunda-fonte-de-regulacao.md) (SER) ·
[ADR-0063](./0063-esus-sao-goncalo-e-analise-de-regras-dos-espelhos.md) (ESUS de São Gonçalo, espelho só leitura)

## Contexto

Quem é agendado pelo SISREG, pelo SER, pelo SERNIT ou pelo ESUS de São Gonçalo precisa levar a guia ao
local do atendimento. Ela só existia no sistema de destino: alguém da equipe entrava, emitia e imprimia.

Sondado em produção em 21/10/2026 (laboratórios `Automais.SER/probe_guia.py`, `Automais.SERNIT/probe_guia.py`,
`Automais.esus_saocongalo/probe_comprovante.py`; protocolo em `docs/ser.md §13`,
`Automais.SERNIT/docs/APRENDIZADOS.md §10`, `Automais.esus_saocongalo/docs/APRENDIZADOS.md §5.4`):

| | Como sai | Efeito no sistema de destino |
|---|---|---|
| **SER** | Menu da linha *Emitir CHAVE DE AUTORIZAÇÃO DO AGENDAMENTO - Nª via* → POST comum do `form0` → **PDF** "Autorização de Consulta/Exame Resumida" | **gasta uma via** (o rótulo passa de 2ª a 3ª); não entra na trilha |
| **SERNIT** | O mesmo item → mesmo PDF. Há também *Emitir Autorização Completa*, que não conta via, mas não traz a chave nem o preparo e traz CID | **gasta uma via**; não entra na trilha |
| **ESUS SG** | `obter-comprovante-exame-agendamento-em-lote-com-preparo-subprocedimentos` devolve **JSON**; o front deles monta um cupom no navegador | nenhum — o "impresso = SIM" é outra chamada (`cadastrar-comprovante-impressao-lote`), que **não** fazemos |
| **SISREG** | O "Imprimir" da ficha (`cons_marcados_reg` EXIBIR_FICHA, ou `gerenciador_solicitacao` VISUALIZAR_FICHA) imprime no navegador a `div.fichaAmbulatorial` da mesma página onde se lê a chave de confirmação | nenhum; custa 1–2 requisições do orçamento anti-robô |

## Decisão

1. **Uma tabela que é a fila: `guia_agendamento`.** Uma linha por agendamento **de amanhã em diante**,
   criada **vazia, sem arquivo**, assim que o agendamento aparece; "quantas faltam" é contar as pendentes. Âncora por
   sistema, exatamente uma (CHECK): `agendamento_externo_id` no SER/SERNIT/ESUS (o agendamento já é
   pedido × data, ADR-0072) ou `solicitacao_id` + `data_atendimento` no SISREG (a remarcação muda a
   data da mesma solicitação). Situação `Pendente` → `Obtida` | `Falhou` (5 tentativas, espera
   crescente) | `Descartada` (o agendamento deixou de valer, ou chegou o dia do atendimento, antes de a
   guia vir — no SER/SERNIT isso evita gastar via à toa). No dia a guia chega tarde, e o SER/SERNIT já tiram
   o pedido de "Agendada" quando o paciente chega: na primeira rodada em produção (21/10/2026) as buscas do
   dia falharam, e só no SISREG eram 1.099 do dia, que tomariam o ritmo inteiro da fila.
2. **Um arquivo só, no acervo do paciente.** O PDF entra em `documento_paciente` com a origem
   `GuiaAgendamento` (5), já aceito. A tela do pedido (quadro *Contato com o paciente*), o cartão do
   agendamento no app e a lista de documentos (painel e app) apontam para o mesmo documento. A equipe
   **não edita nem exclui** a guia.
3. **Ritmo por sistema, configurável e no mesmo formato para os quatro** (`guia_fila_configuracao`, uma
   linha por sistema): ligada, **guias por hora**, **janela do dia** (Brasília; início = fim é o dia
   inteiro; janela que vira o dia vale) e uma **pausa** (`pausada_ate` + motivo) que o próprio sistema
   aciona. O mesmo quadro aparece na tela de configuração de cada sistema (SISREG, SER, SERNIT, ESUS),
   com quantas faltam, quantas vieram hoje, quantas falharam e, se a fila não anda, **por quê**.
   Padrão: o dia inteiro; 60/h no SER/SERNIT/ESUS, 30/h no SISREG. Salvar tira a pausa.
   - Mora em tabela própria, e não no `ParametrosJson` da credencial de cada provedor: aquele JSON é
     regravado inteiro ao salvar a credencial (achado em 21/10/2026 — salvar a credencial do SER/SERNIT
     apaga `varreduraAtiva`/`varreduraHoraLocal`; o cartão do SISREG apaga fila/escalas/lote/indicadores).
4. **Um processo, uma linha por sistema** (`FilaGuiasScheduler`): uma linha enfileira a cada 10 min (só
   banco) e cada sistema busca uma guia por vez, esperando `3600 / guias por hora` entre uma e outra —
   o SISREG devagar não segura o SER. Sistema novo é mais um `IBaixadorGuia` registrado. Antes de cada
   busca vale a mesma régua que a tela mostra: desligada, pausada, fora da janela, ou o **impedimento**
   do baixador — a varredura do sistema rodando (sessão única no SER/SERNIT/ESUS); no SISREG, a
   chave-mestra do sincronismo automático desligada, outro motor do SISREG vivo ou o orçamento automático
   com menos de 40 de folga.
5. **Uma vez, sem contar vias.** A busca é idempotente pelo acervo (`origem_referencia =
   guia-agendamento:{id}`): se a emissão deu certo e a marcação da linha falhou, a próxima vez **liga** o
   documento existente em vez de emitir de novo. O número da via impresso na guia é o que o sistema deu.
6. **Por sistema:**
   - **SER e SERNIT:** o PDF da chave de autorização, como o sistema entrega, pela porta de escrita
     nomeada da sessão (gasta via). A "Autorização Completa" do SERNIT fica de fora.
   - **ESUS SG:** só leitura, por uma porta nomeada da sessão — a trava geral continua recusando
     "comprovante". O PDF é montado aqui (QuestPDF) com os campos do cupom e diz que foi montado a partir
     do ESUS. O `eap_id` do agendamento passa a ser guardado no espelho (`esussg_solicitacao.agendamento_id_esus`).
   - **SISREG:** a ficha, lida pelo **mesmo comando da chave de confirmação** (`LerFichaAsync`, que
     também guarda a chave se ela ainda não estava), vira PDF aqui com as seções da tela e a chave em
     destaque. Auditoria própria (`FichaSisregLidaParaGuia`). A credencial é a dedicada do sincronismo;
     **CAPTCHA pausa a fila do SISREG por 24 horas** (o CAPTCHA é do usuário, não da guia).
7. **A guia vai com a chave, e o paciente a vê no app antes do dia** (decisão do dono, 21/10/2026). A
   regra "chave só no dia do atendimento" do botão *Visualizar chave de acesso* do app continua valendo
   para aquele botão; a guia, que é o papel que o paciente receberia no balcão, não segue essa regra.

## Consequências

- O primeiro deploy enfileira a guia de todos os agendamentos futuros (cerca de 165 do SER/SERNIT/ESUS em
  10/10/2026, mais os do SISREG). Cada uma do SER e do SERNIT gasta uma via lá; aceito pelo dono.
- Quando a unidade imprimir no balcão depois disso, sai a via seguinte: o contador do SER/SERNIT deixa de
  dizer quantas vezes o paciente pediu cópia.
- O SISREG ganha um consumidor a mais do orçamento anti-robô — o menos urgente, que cede a todos.
- Banco: tabelas `guia_agendamento` e `guia_fila_configuracao`; coluna `esussg_solicitacao.agendamento_id_esus`;
  valor novo no enum persistido `OrigemDocumentoPaciente`.
- Desligável inteiro por configuração do servidor (`GuiasAgendamento:Habilitado=false`).
