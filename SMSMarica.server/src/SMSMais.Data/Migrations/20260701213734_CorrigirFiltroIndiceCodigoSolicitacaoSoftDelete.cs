using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class CorrigirFiltroIndiceCodigoSolicitacaoSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_solicitacao_exame_codigo_solicitacao",
                schema: "smsmarica",
                table: "solicitacao_exame");

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_exame_codigo_solicitacao",
                schema: "smsmarica",
                table: "solicitacao_exame",
                column: "codigo_solicitacao",
                unique: true,
                filter: "codigo_solicitacao IS NOT NULL AND codigo_solicitacao <> '0000' AND excluido_em IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_solicitacao_exame_codigo_solicitacao",
                schema: "smsmarica",
                table: "solicitacao_exame");

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_exame_codigo_solicitacao",
                schema: "smsmarica",
                table: "solicitacao_exame",
                column: "codigo_solicitacao",
                unique: true,
                filter: "codigo_solicitacao IS NOT NULL AND codigo_solicitacao <> '0000'");
        }
    }
}
