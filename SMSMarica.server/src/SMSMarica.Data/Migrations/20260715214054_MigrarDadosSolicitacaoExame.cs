using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <summary>
    /// Migração de DADOS do split (ADR-0021), fase "migrate" do expand→migrate→verify→contract.
    /// Copia cada linha de <c>solicitacao_exame</c> para a espinha <c>solicitacao</c> (regulação,
    /// id NOVO) + o satélite <c>exame_imagem</c> (execução, id PRESERVADO = id antigo, para que as
    /// FKs dependentes continuem válidas com um simples rename de coluna).
    ///
    /// NÃO dropa <c>solicitacao_exame</c> — isso é a migração de "contract", aplicada só após o
    /// verify gate, com backup. Reversível: <c>Down</c> esvazia as duas tabelas (neste ponto elas só
    /// contêm o que esta migração inseriu — nada de produção escreve nelas ainda).
    /// </summary>
    public partial class MigrarDadosSolicitacaoExame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- Mapa old(exame) -> novo(solicitacao). Vive só nesta transação de migração.
CREATE TEMP TABLE _map_solic ON COMMIT DROP AS
SELECT se.id AS exame_id, gen_random_uuid() AS sol_id
FROM smsmarica.solicitacao_exame se;

-- 1) Espinha (regulação). Categoria = 2 (Imagem): todo o legado é exame de imagem.
INSERT INTO smsmarica.solicitacao (
    id, paciente_id, categoria,
    procedimento_sigtap_codigo, procedimento_texto, especialidade_texto,
    unidade_executante_id, unidade_solicitante_id,
    solicitante_usuario_id, solicitante_nome, solicitante_num_conselho,
    solicitante_uf_conselho, solicitante_cpf, solicitante_conselho,
    codigo_solicitacao, chave_confirmacao, raw_sisreg, justificativa, observacoes,
    status, prioridade,
    data_solicitacao, data_regulacao, data_agendada,
    status_confirmacao, confirmado_em, confirmado_canal, confirmacao_cancelada_em, motivo_cancelamento_paciente,
    autorizado_em, autorizado_por,
    cancelado_em, cancelado_por_usuario_id, motivo_cancelamento,
    criado_em, criado_por, atualizado_em, atualizado_por, excluido_em, excluido_por
)
SELECT
    m.sol_id, se.paciente_id, 2,
    NULL, NULL, NULL,
    se.unidade_id, se.unidade_solicitante_id,
    se.solicitante_usuario_id, se.solicitante_nome, se.solicitante_num_conselho,
    se.solicitante_uf_conselho, se.solicitante_cpf, se.solicitante_conselho,
    se.codigo_solicitacao, se.chave_confirmacao, se.raw_sisreg, se.justificativa, se.observacoes,
    -- Status de REGULAÇÃO derivado do status de execução legado:
    --   6 Cancelada -> 4 Cancelada; 3/4/5 (EmExecucao/Realizada/Laudada) -> 3 Realizada;
    --   com data agendada -> 2 Agendada; senão -> 1 Solicitada.
    CASE
        WHEN se.status = 6 THEN 4
        WHEN se.status IN (3, 4, 5) THEN 3
        WHEN se.data_agendada IS NOT NULL THEN 2
        ELSE 1
    END,
    se.prioridade,
    se.data_solicitacao, se.data_regulacao, se.data_agendada,
    se.status_confirmacao, se.confirmado_em, se.confirmado_canal, se.confirmacao_cancelada_em, se.motivo_cancelamento_paciente,
    se.autorizado_em, se.autorizado_por,
    se.cancelado_em, se.cancelado_por_usuario_id, se.motivo_cancelamento,
    se.criado_em, se.criado_por, se.atualizado_em, se.atualizado_por, se.excluido_em, se.excluido_por
FROM smsmarica.solicitacao_exame se
JOIN _map_solic m ON m.exame_id = se.id;

-- 2) Satélite (execução de imagem). id PRESERVADO (= id antigo); status legado copiado verbatim.
INSERT INTO smsmarica.exame_imagem (
    id, solicitacao_id, tipo_exame_id,
    accession_number, study_instance_uid, worklist_item_uid,
    status, iniciado_em, realizado_em, data_estudo,
    erro_integracao_pacs, tentativas_envio, ultima_tentativa_em, proxima_tentativa_em,
    imagens_preparadas_em, imagens_preparacao_tentativas,
    criado_em, criado_por, atualizado_em, atualizado_por, excluido_em, excluido_por
)
SELECT
    se.id, m.sol_id, se.tipo_exame_id,
    se.accession_number, se.study_instance_uid, se.worklist_item_uid,
    se.status, se.iniciado_em, se.realizado_em, se.data_estudo,
    se.erro_integracao_pacs, se.tentativas_envio, se.ultima_tentativa_em, se.proxima_tentativa_em,
    se.imagens_preparadas_em, se.imagens_preparacao_tentativas,
    se.criado_em, se.criado_por, se.atualizado_em, se.atualizado_por, se.excluido_em, se.excluido_por
FROM smsmarica.solicitacao_exame se
JOIN _map_solic m ON m.exame_id = se.id;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Neste ponto do plano (antes do contract) as duas tabelas só contêm o que o Up inseriu.
            migrationBuilder.Sql("TRUNCATE smsmarica.exame_imagem, smsmarica.solicitacao;");
        }
    }
}
