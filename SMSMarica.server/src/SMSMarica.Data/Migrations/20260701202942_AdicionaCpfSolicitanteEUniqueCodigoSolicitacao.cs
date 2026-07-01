using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaCpfSolicitanteEUniqueCodigoSolicitacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "solicitante_cpf",
                schema: "smsmarica",
                table: "solicitacao_exame",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_exame_codigo_solicitacao",
                schema: "smsmarica",
                table: "solicitacao_exame",
                column: "codigo_solicitacao",
                unique: true,
                filter: "codigo_solicitacao IS NOT NULL AND codigo_solicitacao <> '0000'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_solicitacao_exame_codigo_solicitacao",
                schema: "smsmarica",
                table: "solicitacao_exame");

            migrationBuilder.DropColumn(
                name: "solicitante_cpf",
                schema: "smsmarica",
                table: "solicitacao_exame");
        }
    }
}
