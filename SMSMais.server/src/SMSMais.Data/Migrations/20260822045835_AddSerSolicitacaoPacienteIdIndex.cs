using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSerSolicitacaoPacienteIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_ser_solicitacao_paciente_id",
                schema: "smsmarica",
                table: "ser_solicitacao",
                column: "paciente_id",
                filter: "paciente_id IS NOT NULL AND excluido_em IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ser_solicitacao_paciente_id",
                schema: "smsmarica",
                table: "ser_solicitacao");
        }
    }
}
