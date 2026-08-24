using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSolicitanteConselho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "solicitante_conselho",
                schema: "smsmarica",
                table: "solicitacao_exame",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "CRM");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "solicitante_conselho",
                schema: "smsmarica",
                table: "solicitacao_exame");
        }
    }
}
