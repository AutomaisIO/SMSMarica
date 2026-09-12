# Base "Atendimento" — visão geral (LEIA PRIMEIRO)

Esta base é o **banco do próprio SMSMais** (PostgreSQL, dialeto Postgres), visto por uma conta que
só enxerga o **atendimento ao cidadão**: as conversas de WhatsApp com os pacientes, o conteúdo das
mensagens, o robô de atendimento e os avisos automáticos (confirmação de agendamento, exame, laudo).
Serve para perguntas como "sobre o que os pacientes mais reclamaram esta semana?", "quantas
conversas ficaram sem resposta?", "o que a paciente X conversou com a gente?", "o robô resolve
quantas sozinho?".

## Recorte de acesso (importante para o SQL)

- A conta **só tem SELECT nas tabelas abaixo e, em algumas, só nas colunas listadas**. **Nunca use
  `SELECT *`**. "permission denied" = coluna/tabela fora do recorte: use outra documentada.
- Sempre qualifique o schema: `smsmarica.<tabela>`, `fhir.patient`.
- Datas `timestamptz` em UTC. Dia/hora de Maricá: `(coluna AT TIME ZONE 'America/Sao_Paulo')`.
- Busca em texto sem acento/caixa: `smsmarica.unaccent(lower(conteudo)) LIKE '%termo%'`.
- Use `LIMIT`; prefira contagens e agrupamentos.

## O conteúdo das conversas é sensível (regra forte)

- É a fala do cidadão: pode ter queixa, doença, telefone, endereço, CPF. **Resuma** e **agregue**:
  temas, contagens, exemplos curtos. Não despeje conversas inteiras.
- Transcreva mensagens **só** quando o operador pedir uma conversa ou um paciente específico. Mesmo
  assim, mostre as mensagens necessárias (com `LIMIT`) e **mascare CPF** (só os 3 primeiros dígitos).
- Para "sobre o que falam", leia uma amostra (`ORDER BY random() LIMIT 200` das mensagens de
  entrada do período), agrupe por tema com o seu próprio entendimento e diga que foi por amostra.
- Mensagens de saída do tipo template são avisos padronizados do sistema, não conversa.

## Mapa

| Assunto | Tabela | Detalhes |
|---|---|---|
| Conversas (uma por contato em atendimento) | `smsmarica.conversa` | `10-conversas.md` |
| Mensagens (texto de cada mensagem) | `smsmarica.whatsapp_mensagem` | `10-conversas.md` |
| Quem pegou/transferiu/resolveu | `smsmarica.conversa_evento` | `10-conversas.md` |
| Robô de atendimento | `smsmarica.robo_tarefa`, `smsmarica.robo_acao`, `smsmarica.robo_assunto` | `20-robo-e-avisos.md` |
| Avisos automáticos ao paciente | `smsmarica.comunicacao_paciente` | `20-robo-e-avisos.md` |
| Agendamento a que a conversa se refere | `smsmarica.solicitacao` (colunas limitadas) | `20-robo-e-avisos.md` |
| Operadores (atendentes) | `smsmarica.usuario` (`id, nome_completo`) | |
| Unidades | `smsmarica.unidade` (`id, nome, cnes, externa, ativo, endereco_bairro, endereco_cidade, latitude, longitude`) | |
| Paciente | `fhir.patient` (`id, nome, cpf, cns, cns_todos, nascimento, telefone, is_deleted`) | |

Perguntas sobre **fila, regulação, SISREG/SER/SERNIT** são da base **Regulação**, não desta. Se o
operador perguntar isso aqui, diga que esta base trata das conversas e sugira abrir a Regulação.
