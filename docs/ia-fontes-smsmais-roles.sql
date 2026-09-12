-- =====================================================================================
-- Consulta Inteligente — contas SÓ-LEITURA das bases "Regulação" e "Atendimento".
--
-- As duas bases são o PRÓPRIO banco do SMSMais. O que recorta o que a IA enxerga é a conta:
-- SELECT só nas tabelas/colunas abaixo (as mesmas descritas em
-- SMSMais.server/src/SMSMais.Core/Inteligencia/Conhecimento/Bases/{regulacao,atendimento}/).
-- Mudou uma lista aqui? Mude o .md correspondente — e vice-versa.
--
-- NÃO roda sozinho (não é migration): é aplicado à mão, por quem administra o banco, UMA vez
-- por instância. Rodar com psql, trocando as senhas:
--   psql "<conexão doadmin>" -v senha_regulacao='...' -v senha_atendimento='...' -f ia-fontes-smsmais-roles.sql
-- Depois cadastrar as duas bases na tela Configuração IA → Bases: tipo Regulação / Atendimento,
-- dialeto PostgreSQL, host/porta/database do banco, usuário ia_regulacao / ia_atendimento.
--
-- Travas independentes além desta conta: SqlReadOnlyGuard + transação READ ONLY (PostgresFonte),
-- auditoria obrigatória em ia_consulta (ProxySqlEndpoint), permissão InteligenciaAtendimento (64).
-- =====================================================================================

\set ON_ERROR_STOP on
BEGIN;

-- ---------------------------------------------------------------- contas
CREATE ROLE ia_regulacao LOGIN PASSWORD :'senha_regulacao'
  NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION CONNECTION LIMIT 5;
CREATE ROLE ia_atendimento LOGIN PASSWORD :'senha_atendimento'
  NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION CONNECTION LIMIT 5;

-- Mesmo sem a aplicação pedir, a sessão destas contas nasce só-leitura e com teto de tempo.
ALTER ROLE ia_regulacao   SET default_transaction_read_only = on;
ALTER ROLE ia_regulacao   SET statement_timeout = '30s';
ALTER ROLE ia_regulacao   SET idle_in_transaction_session_timeout = '60s';
ALTER ROLE ia_atendimento SET default_transaction_read_only = on;
ALTER ROLE ia_atendimento SET statement_timeout = '30s';
ALTER ROLE ia_atendimento SET idle_in_transaction_session_timeout = '60s';

GRANT USAGE ON SCHEMA smsmarica, fhir TO ia_regulacao, ia_atendimento;
-- unaccent (busca sem acento) está no schema smsmarica; EXECUTE já é de PUBLIC por padrão.

-- ---------------------------------------------------------------- comuns às duas
GRANT SELECT (id, nome, cnes, externa, ativo, endereco_bairro, endereco_cidade, latitude, longitude)
  ON smsmarica.unidade TO ia_regulacao, ia_atendimento;
GRANT SELECT (id, nome, cpf, cns, cns_todos, nascimento, telefone, is_deleted)
  ON fhir.patient TO ia_regulacao, ia_atendimento;

-- ---------------------------------------------------------------- REGULAÇÃO
-- solicitacao SEM chave_confirmacao (prova de comparecimento), raw_sisreg e CPFs de profissionais.
GRANT SELECT (id, paciente_id, categoria, procedimento_sigtap_codigo, procedimento_codigo_sisreg,
  procedimento_texto, especialidade_texto, unidade_executante_id, unidade_solicitante_id,
  solicitante_nome, codigo_solicitacao, justificativa, observacoes, cid_codigo, status, prioridade,
  data_solicitacao, data_regulacao, data_agendada, status_confirmacao, confirmado_em,
  confirmado_canal, confirmacao_cancelada_em, motivo_cancelamento_paciente, autorizado_em,
  cancelado_em, motivo_cancelamento, profissional_executante_nome, criado_em, atualizado_em, excluido_em)
  ON smsmarica.solicitacao TO ia_regulacao;

GRANT SELECT ON smsmarica.sisreg_fila_pendente TO ia_regulacao;
GRANT SELECT ON smsmarica.sisreg_alteracao_agenda TO ia_regulacao;
GRANT SELECT ON smsmarica.procedimento_sigtap TO ia_regulacao;
GRANT SELECT (id, codigo, nome, grupo, codigo_sigtap) ON smsmarica.sisreg_procedimento_sigtap TO ia_regulacao;
GRANT SELECT (id, codigo_escala, unidade_id, cnes, unidade_nome_sisreg, profissional_nome, cbo_codigo,
  cbo_descricao, procedimento_codigo, procedimento_nome, procedimento_sigtap, eh_grupo, dia_semana,
  hora_inicio, hora_fim, vigencia_inicio, vigencia_fim, vagas_primeira_vez, vagas_retorno,
  vagas_reserva, vagas_total, status, agenda_local, visto_em, ausente)
  ON smsmarica.sisreg_escala TO ia_regulacao;

GRANT SELECT ON smsmarica.ser_solicitacao, smsmarica.sernit_solicitacao TO ia_regulacao;
GRANT SELECT (id, ser_solicitacao_id, data_evento, evento, estado_anterior, estado_atual,
  central_regulacao, unidade_executora, usuario, lotacao_evento, observacao, capturado_em)
  ON smsmarica.ser_evento TO ia_regulacao;
GRANT SELECT (id, sernit_solicitacao_id, data_evento, evento, estado_anterior, estado_atual,
  central_regulacao, unidade_executora, usuario, lotacao_evento, observacao, capturado_em)
  ON smsmarica.sernit_evento TO ia_regulacao;
GRANT SELECT (id, tipo, valor, rotulo, ambulatorio_estadual) ON smsmarica.ser_catalogo_recurso TO ia_regulacao;
GRANT SELECT (id, tipo, valor, rotulo) ON smsmarica.sernit_catalogo_recurso TO ia_regulacao;

GRANT SELECT (id, nome_canonico, tipo, procedimento_sigtap_id, ativo)
  ON smsmarica.regulacao_procedimento TO ia_regulacao;
GRANT SELECT (id, procedimento_id, sistema, chave_externa, rotulo_externo, ramo, vinculo, ativo)
  ON smsmarica.regulacao_procedimento_origem TO ia_regulacao;
GRANT SELECT (id, numero_local, fluxo, unidade_solicitante_id, unidade_em_nome_de_id, paciente_id,
  paciente_cpf, paciente_cns, paciente_nome, procedimento_id, sistema_destino, status, status_motivo,
  numero_externo, enviado_em, solicitacao_id, ser_solicitacao_id, sernit_solicitacao_id, observacoes,
  criado_em, atualizado_em, excluido_em)
  ON smsmarica.regulacao_solicitacao TO ia_regulacao;
GRANT SELECT (id, solicitacao_id, tipo, status_anterior, status_novo, usuario_nome, papel, criado_em)
  ON smsmarica.regulacao_evento TO ia_regulacao;
GRANT SELECT ON smsmarica.regulacao_solicitacao_destino TO ia_regulacao;

-- ---------------------------------------------------------------- ATENDIMENTO
GRANT SELECT ON smsmarica.conversa, smsmarica.conversa_evento, smsmarica.whatsapp_mensagem,
  smsmarica.robo_acao TO ia_atendimento;
GRANT SELECT (id, tipo, finalidade, solicitacao_id, paciente_id, telefone, status, motivo_falha,
  mensagem_whatsapp_id, tentativas, enviado_em, entregue_em, lido_em, visualizado_em, criado_em,
  origem, enviado_por)
  ON smsmarica.comunicacao_paciente TO ia_atendimento;
GRANT SELECT (id, conversa_id, mensagem_whatsapp_id, paciente_id, robo_assunto_id, status,
  confianca_ultima, tentativas, erro, criado_em, atualizado_em, custo_usd, tokens_entrada, tokens_saida)
  ON smsmarica.robo_tarefa TO ia_atendimento;
GRANT SELECT (id, nome, descricao, ativo, padrao) ON smsmarica.robo_assunto TO ia_atendimento;
GRANT SELECT (id, nome_completo) ON smsmarica.usuario TO ia_atendimento;
GRANT SELECT (id, paciente_id, codigo_solicitacao, procedimento_texto, categoria, status, data_agendada,
  status_confirmacao, confirmado_em, confirmado_canal, unidade_executante_id, cancelado_em, excluido_em)
  ON smsmarica.solicitacao TO ia_atendimento;

COMMIT;

-- Conferência (deve listar só o que está acima):
-- SELECT grantee, table_schema, table_name, string_agg(column_name, ', ')
--   FROM information_schema.column_privileges
--  WHERE grantee IN ('ia_regulacao','ia_atendimento') GROUP BY 1,2,3 ORDER BY 1,2,3;
