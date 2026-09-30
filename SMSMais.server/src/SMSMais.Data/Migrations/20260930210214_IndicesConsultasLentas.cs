using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <summary>
    /// A raia "Cancelados" do Painel de Início deixa de varrer a tabela de solicitações.
    ///
    /// O contador (<c>PainelInicioService.CanceladosAsync</c>) roda a cada 60 s para cada usuário
    /// logado — é o badge do item "Início" no menu — e cada chamada fazia duas passadas no heap
    /// inteiro de <c>smsmarica.solicitacao</c> (~934 MB, ~1 mi de linhas): o <c>count</c> e o
    /// <c>ORDER BY confirmacao_cancelada_em DESC LIMIT 5</c>. O filtro seletivo é
    /// <c>status_confirmacao = 3</c> (Cancelada, ~0,01% das linhas), e não havia índice nele. Foi a
    /// maior fonte de leitura de disco do banco medida em 30/09/2026.
    ///
    /// Índice parcial com o MESMO predicado da consulta: <c>StatusConfirmacao == Cancelada</c> e
    /// <c>EmAberto</c> = Solicitada (1) / Agendada (2) e <c>ExcluidoEm == null</c>. Se o
    /// <c>EmAberto</c> do PainelInicioService ou os valores dos enums mudarem, o índice deixa de
    /// ser usado EM SILÊNCIO — mudar os dois juntos.
    ///
    /// Idempotente (IF NOT EXISTS): em produção o índice já foi criado à mão com
    /// CREATE INDEX CONCURRENTLY (sem travar escrita), então aqui vira no-op; numa instância nova a
    /// tabela nasce vazia e o índice é instantâneo. Mesmo padrão da AddPatientTrgmSearch do FHIR.
    /// </summary>
    /// <inheritdoc />
    public partial class IndicesConsultasLentas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_solicitacao_cancelados_em_aberto
    ON smsmarica.solicitacao (confirmacao_cancelada_em DESC)
    INCLUDE (unidade_executante_id, unidade_solicitante_id)
    WHERE status_confirmacao = 3 AND status IN (1, 2) AND excluido_em IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS smsmarica.ix_solicitacao_cancelados_em_aberto;");
        }
    }
}
