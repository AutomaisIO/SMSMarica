# Base "Regulação" — visão geral (LEIA PRIMEIRO)

Esta base é o **banco do próprio SMSMais** (PostgreSQL, dialeto Postgres), visto por uma conta
que só enxerga o que é de **regulação**: pedidos, filas, agendas e agendamentos de três sistemas.

## Interno × externo — o vocabulário do operador

| O operador diz | Significa | Onde está |
|---|---|---|
| **interno**, "nosso", "do município", SISREG | Regulação de Maricá, no **SISREG** | `smsmarica.solicitacao` (agendamentos importados), `smsmarica.sisreg_fila_pendente` (fila de espera), `smsmarica.sisreg_escala` (vagas/agendas) |
| **externo**, "do Estado", "de Niterói", SER, SERNIT | Regulação feita por outro ente | **SER** (SES-RJ): `smsmarica.ser_solicitacao` · **SERNIT** (Niterói): `smsmarica.sernit_solicitacao` |
| "a regulação", "tudo", "no geral" | Os três juntos | Some/una as três fontes (ver `40-cruzamentos.md`) |

- **interno = só SISREG.** **externo = SER + SERNIT** (os dois, a menos que o operador cite um).
- Pode **combinar**: "quem está na fila interna e também na externa?", "compare a espera do SISREG
  com a do SER para cardiologia".

## Quando PERGUNTAR antes de consultar (regra forte)

Se a resposta muda conforme o sistema e o operador **não disse qual**, pergunte em uma frase curta,
em linguagem de negócio, e **espere a resposta** — não chute:

> "Você quer a fila **interna** (SISREG), a **externa** (SER e SERNIT) ou as duas somadas?"

Pergunte também quando o **procedimento** for ambíguo (ex.: "eco" pode ser ecocardiograma ou
ecografia/ultrassom; "cardio" pode ser consulta ou exame) ou o **período** for indispensável e não
tiver sido dito. **Não pergunte** o que dá para decidir: se ele citou o sistema, o procedimento e o
período, ou se a pergunta só faz sentido num sistema (ex.: "vagas" e "escalas" são só SISREG),
responda direto e diga o recorte usado ("considerei só o SISREG").

## O que existe (mapa rápido)

| Assunto | Tabela | Detalhes |
|---|---|---|
| Agendamentos/solicitações do SISREG executados na rede | `smsmarica.solicitacao` | `10-sisreg.md` |
| Fila de espera do SISREG (quem ainda não foi agendado) | `smsmarica.sisreg_fila_pendente` | `10-sisreg.md` |
| Agendas/vagas oferecidas no SISREG (escalas) | `smsmarica.sisreg_escala` | `10-sisreg.md` |
| Remarcações/cancelamentos detectados | `smsmarica.sisreg_alteracao_agenda` | `10-sisreg.md` |
| Pedidos no SER (Estado) e histórico | `smsmarica.ser_solicitacao`, `smsmarica.ser_evento` | `20-ser-sernit.md` |
| Pedidos no SERNIT (Niterói) e histórico | `smsmarica.sernit_solicitacao`, `smsmarica.sernit_evento` | `20-ser-sernit.md` |
| Catálogo único de procedimentos (liga os 3 sistemas) | `smsmarica.regulacao_procedimento`, `smsmarica.regulacao_procedimento_origem` | `30-catalogo-e-pre-regulacao.md` |
| Pré-regulação feita no SMSMais | `smsmarica.regulacao_solicitacao` (+ `_evento`, `_destino`) | `30-catalogo-e-pre-regulacao.md` |
| Unidades de saúde | `smsmarica.unidade` | abaixo |
| Paciente (identidade) | `fhir.patient` | abaixo |

## Recorte de acesso (importante para o SQL)

- A conta **só tem SELECT em algumas tabelas e, em algumas, só em algumas colunas**. **Nunca use
  `SELECT *`** — liste as colunas descritas nestes documentos. "permission denied" = coluna/tabela
  fora do recorte: troque por outra coluna documentada.
- Sempre qualifique o schema: `smsmarica.<tabela>`, `fhir.patient`.
- Linhas "apagadas" têm `excluido_em IS NOT NULL` — filtre `excluido_em IS NULL` onde a coluna existir.
- Datas `timestamptz` estão em UTC. Para dia/hora de Maricá: `(coluna AT TIME ZONE 'America/Sao_Paulo')`.
  Colunas `date` (ex.: `data_solicitacao`) já são o dia local.
- Busca por nome sem acento/caixa: `smsmarica.unaccent(lower(x)) LIKE smsmarica.unaccent(lower('%termo%'))`.
- Use `LIMIT`. Prefira `count(*)`, `group by`, percentis (`percentile_cont(0.5) within group (order by ...)`).

## Tabelas comuns

`smsmarica.unidade` — colunas liberadas: `id, nome, cnes, externa, ativo, endereco_bairro,
endereco_cidade, latitude, longitude`. Liga em `solicitacao.unidade_executante_id`,
`solicitacao.unidade_solicitante_id`, `sisreg_escala.unidade_id`.

`fhir.patient` — colunas liberadas: `id, nome, cpf, cns, cns_todos, nascimento, telefone, is_deleted`.
É o cadastro único do cidadão. `solicitacao.paciente_id`, `ser_solicitacao.paciente_id`,
`sernit_solicitacao.paciente_id` e `regulacao_solicitacao.paciente_id` apontam para `fhir.patient.id`.
A fila do SISREG só tem **CNS**: case com `p.cns = f.cns` (rápido). `f.cns = ANY(p.cns_todos)` pega
CNS antigos mas é **lento** (varre 380 mil cadastros) — use só para poucos pacientes.

## PII

Os dados são reais. Para panoramas, **agregue**. Nome/CPF/CNS/telefone só quando o operador pedir
uma pessoa ou uma lista nominal para agir (ex.: "quem espera há mais de 1 ano no ECO?"), e mesmo
assim com `LIMIT` e só as colunas necessárias.
