using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class FollowUpEstruturado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "followup_categoria",
                schema: "smsmarica",
                table: "sernit_evento",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "followup_regras_hash",
                schema: "smsmarica",
                table: "sernit_evento",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tipo_evento",
                schema: "smsmarica",
                table: "sernit_evento",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "followup_categoria",
                schema: "smsmarica",
                table: "ser_evento",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "followup_regras_hash",
                schema: "smsmarica",
                table: "ser_evento",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tipo_evento",
                schema: "smsmarica",
                table: "ser_evento",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill do verbo tipado — a mesma regra de ClassificadorEventoRegulacao.TipoDoVerbo,
            // em SQL. Roda antes dos índices para não os manter durante o UPDATE. Um único UPDATE
            // por tabela: é uma coluna inteira nova, sem rewrite de linha além do inevitável.
            // A CATEGORIA do FollowUP fica de fora de propósito: depende das regras editáveis da
            // configuração, e quem a preenche é o FollowUpClassificacaoWorker, em lotes.
            foreach (var tabela in new[] { "ser_evento", "sernit_evento" })
            {
                migrationBuilder.Sql($"""
                    UPDATE smsmarica.{tabela}
                    SET tipo_evento = CASE
                        WHEN evento ILIKE '%follow%up%' THEN 2
                        WHEN evento ILIKE 'solicit%'    THEN 1
                        WHEN evento ILIKE 'pendenc%'    THEN 3
                        WHEN evento ILIKE 'cancel%'     THEN 4
                        WHEN evento ILIKE 'agend%'      THEN 5
                        ELSE 99
                    END
                    WHERE tipo_evento = 0;
                    """);
            }

            migrationBuilder.CreateIndex(
                name: "ix_sernit_evento_followup_regras_hash",
                schema: "smsmarica",
                table: "sernit_evento",
                column: "followup_regras_hash",
                filter: "tipo_evento = 2");

            migrationBuilder.CreateIndex(
                name: "ix_sernit_evento_solicitacao_tipo_data",
                schema: "smsmarica",
                table: "sernit_evento",
                columns: new[] { "sernit_solicitacao_id", "tipo_evento", "data_evento" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_ser_evento_followup_regras_hash",
                schema: "smsmarica",
                table: "ser_evento",
                column: "followup_regras_hash",
                filter: "tipo_evento = 2");

            migrationBuilder.CreateIndex(
                name: "ix_ser_evento_solicitacao_tipo_data",
                schema: "smsmarica",
                table: "ser_evento",
                columns: new[] { "ser_solicitacao_id", "tipo_evento", "data_evento" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sernit_evento_followup_regras_hash",
                schema: "smsmarica",
                table: "sernit_evento");

            migrationBuilder.DropIndex(
                name: "ix_sernit_evento_solicitacao_tipo_data",
                schema: "smsmarica",
                table: "sernit_evento");

            migrationBuilder.DropIndex(
                name: "ix_ser_evento_followup_regras_hash",
                schema: "smsmarica",
                table: "ser_evento");

            migrationBuilder.DropIndex(
                name: "ix_ser_evento_solicitacao_tipo_data",
                schema: "smsmarica",
                table: "ser_evento");

            migrationBuilder.DropColumn(
                name: "followup_categoria",
                schema: "smsmarica",
                table: "sernit_evento");

            migrationBuilder.DropColumn(
                name: "followup_regras_hash",
                schema: "smsmarica",
                table: "sernit_evento");

            migrationBuilder.DropColumn(
                name: "tipo_evento",
                schema: "smsmarica",
                table: "sernit_evento");

            migrationBuilder.DropColumn(
                name: "followup_categoria",
                schema: "smsmarica",
                table: "ser_evento");

            migrationBuilder.DropColumn(
                name: "followup_regras_hash",
                schema: "smsmarica",
                table: "ser_evento");

            migrationBuilder.DropColumn(
                name: "tipo_evento",
                schema: "smsmarica",
                table: "ser_evento");
        }
    }
}
