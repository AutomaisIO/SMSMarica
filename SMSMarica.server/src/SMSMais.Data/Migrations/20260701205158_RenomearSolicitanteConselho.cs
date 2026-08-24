using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenomearSolicitanteConselho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "solicitante_uf_crm",
                schema: "smsmarica",
                table: "solicitacao_exame",
                newName: "solicitante_uf_conselho");

            migrationBuilder.RenameColumn(
                name: "solicitante_crm",
                schema: "smsmarica",
                table: "solicitacao_exame",
                newName: "solicitante_num_conselho");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "solicitante_uf_conselho",
                schema: "smsmarica",
                table: "solicitacao_exame",
                newName: "solicitante_uf_crm");

            migrationBuilder.RenameColumn(
                name: "solicitante_num_conselho",
                schema: "smsmarica",
                table: "solicitacao_exame",
                newName: "solicitante_crm");
        }
    }
}
