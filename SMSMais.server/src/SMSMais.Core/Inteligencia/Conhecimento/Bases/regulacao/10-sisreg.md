# SISREG (regulação INTERNA de Maricá)

## `smsmarica.sisreg_fila_pendente` — a fila de espera (quem ainda NÃO tem data)

Espelho da fila do regulador no SISREG, lida inteira todo dia de madrugada. ~73 mil pessoas abertas.
Tabela liberada inteira.

| Coluna | Significado |
|---|---|
| `codigo_solicitacao` | Número da solicitação no SISREG (liga em `solicitacao.codigo_solicitacao`) |
| `data_solicitacao` (date) | Quando o pedido entrou. **Espera = `current_date - data_solicitacao`** (dias) |
| `risco` | Classificação do regulador: **0 = vermelho (mais urgente), 1 = amarelo, 2 = verde, 3 = azul**; nulo = não classificado |
| `paciente_nome`, `cns`, `nome_mae`, `data_nascimento`, `idade_anos`, `telefone`, `municipio` | Paciente como o SISREG mostra |
| `procedimento_nome` | Nome do procedimento no SISREG. **`procedimento_codigo` vem vazio** — filtre por nome |
| `cid_codigo`, `unidade_solicitante` (texto) | CID e quem pediu |
| `situacao` (texto) | `'SOL/PEN/REG'` pendente de regulação · `'SOL/REE/REG'` reenviada (voltou para a fila, mantém a data original) |
| `saiu_em` | **Nulo = ainda na fila (aberta).** Preenchido = saiu |
| `saiu_para` | 1 = saiu porque foi **agendada** · 2 = saiu sem agendar (cancelada/negada/devolvida — o SISREG não diz qual) |
| `primeiro_visto_em`, `ultimo_visto_em` | Quando a nossa leitura viu a pessoa na fila |

- "Quantos na fila de X" = `WHERE saiu_em IS NULL AND procedimento_nome ILIKE '%X%'`.
- "Tempo de espera" = mediana/máximo de `current_date - data_solicitacao` das abertas.
- Um procedimento de **grupo** (nome genérico, ex.: "CONSULTA EM CARDIOLOGIA") e seus itens são nomes
  diferentes; se o operador perguntar pela especialidade, some os nomes que casam e diga quais.

## `smsmarica.solicitacao` — agendamentos/solicitações do SISREG executados na rede

~980 mil linhas desde 2019: tudo que o SISREG marcou para as unidades de Maricá (importado da agenda
de cada unidade executante). Colunas liberadas:

`id, paciente_id, categoria, procedimento_sigtap_codigo, procedimento_codigo_sisreg,
procedimento_texto, especialidade_texto, unidade_executante_id, unidade_solicitante_id,
solicitante_nome, codigo_solicitacao, justificativa, observacoes, cid_codigo, status, prioridade,
data_solicitacao, data_regulacao, data_agendada, status_confirmacao, confirmado_em,
confirmado_canal, confirmacao_cancelada_em, motivo_cancelamento_paciente, autorizado_em,
cancelado_em, motivo_cancelamento, profissional_executante_nome, criado_em, atualizado_em, excluido_em`

| Coluna | Valores |
|---|---|
| `status` | 1 Solicitada (sem data) · 2 **Agendada** · 3 Realizada · 4 Cancelada. **Quase tudo fica em 2 mesmo depois do dia** — "já foi atendido" não sai do status; use `data_agendada < now()` como "o dia já passou" e diga isso |
| `categoria` | 1 Consulta · 2 Imagem · 3 Laboratório · 4 Gráfico/funcional (ECG, EEG, audiometria…) · 5 Endoscopia · 6 Cirurgia · 99 Outro |
| `prioridade` | 1 Eletiva · 2 Prioritária · 3 Urgente |
| `data_agendada` (timestamptz, UTC) | Dia/hora marcado. Dia local: `(data_agendada AT TIME ZONE 'America/Sao_Paulo')::date` |
| `data_solicitacao` (date) / `data_regulacao` (date) | Pedido e autorização. **Tempo até marcar** = `(data_agendada AT TIME ZONE 'America/Sao_Paulo')::date - data_solicitacao` |
| `status_confirmacao` | Resposta do paciente ao aviso de WhatsApp: 1 Pendente · 2 Confirmou · 3 Cancelou |
| `procedimento_texto` | Nome do procedimento como veio do SISREG; `procedimento_codigo_sisreg` é o código interno do SISREG |
| `codigo_solicitacao` | Número do SISREG (`'0000'` ou nulo = criada à mão no SMSMais, não é do SISREG) |

- Unidade: `JOIN smsmarica.unidade u ON u.id = s.unidade_executante_id`.
- Filtre `excluido_em IS NULL`.

## `smsmarica.sisreg_escala` — agendas/vagas oferecidas (escalas)

Uma linha por bloco semanal de agenda (profissional × procedimento × dia da semana × horário).
Colunas liberadas: `id, codigo_escala, unidade_id, cnes, unidade_nome_sisreg, profissional_nome,
cbo_codigo, cbo_descricao, procedimento_codigo, procedimento_nome, procedimento_sigtap, eh_grupo,
dia_semana, hora_inicio, hora_fim, vigencia_inicio, vigencia_fim, vagas_primeira_vez, vagas_retorno,
vagas_reserva, vagas_total, status, agenda_local, visto_em, ausente`.

- `status`: 1 Ativa · 2 Inativa · 3 Expirada · 4 Excluída. Vigente hoje = `status = 1 AND current_date BETWEEN vigencia_inicio AND vigencia_fim AND NOT ausente`.
- `dia_semana`: 0 = domingo … 6 = sábado. Vagas **por semana** do bloco.
- **Vagas da regulação = `vagas_primeira_vez + vagas_reserva`** (retorno fica com a unidade).
- `agenda_local = true`: a própria unidade marca; essas vagas **não passam pela regulação**.
- A vigência NÃO diz quando há vaga livre — só a validade do bloco. Vaga livre ≈ vagas do dia − agendados
  (`solicitacao` com `data_agendada` no dia, mesma unidade e procedimento). Deixe claro que é estimativa.

## `smsmarica.sisreg_alteracao_agenda` — o que o SISREG mudou em agendamentos já importados

Tabela liberada inteira. `tipo`: 1 remarcação (data/hora) · 2 troca de profissional · 3 troca de
procedimento · 4 **sumiu do SISREG** (provável cancelamento). `valor_antes`/`valor_depois` em texto,
`detectada_em`, `tratada_em` (nulo = ninguém tratou), `comunicada_em` (paciente avisado).
Liga em `solicitacao` por `solicitacao_id`.

## Catálogo SISREG

`smsmarica.sisreg_procedimento_sigtap` (`id, codigo, nome, grupo, codigo_sigtap`) — procedimentos do
SISREG vistos na rede. `smsmarica.procedimento_sigtap` — tabela SIGTAP (liberada inteira).
