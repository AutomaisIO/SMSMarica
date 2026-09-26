using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReguaReforcoConfirmacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "orientacao_posto_habilitada",
                schema: "smsmarica",
                table: "confirmacao_configuracao",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "reforco_confirmacao_habilitado",
                schema: "smsmarica",
                table: "confirmacao_configuracao",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "orientacao_posto_habilitada",
                schema: "smsmarica",
                table: "confirmacao_configuracao");

            migrationBuilder.DropColumn(
                name: "reforco_confirmacao_habilitado",
                schema: "smsmarica",
                table: "confirmacao_configuracao");
        }
    }
}
