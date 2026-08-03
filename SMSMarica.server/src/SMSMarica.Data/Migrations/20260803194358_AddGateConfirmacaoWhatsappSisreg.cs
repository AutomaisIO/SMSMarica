using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGateConfirmacaoWhatsappSisreg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "enviar_confirmacao",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "enviar_confirmacao",
                schema: "smsmarica",
                table: "sisreg_procedimento_profissional",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "enviar_confirmacao",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda");

            migrationBuilder.DropColumn(
                name: "enviar_confirmacao",
                schema: "smsmarica",
                table: "sisreg_procedimento_profissional");
        }
    }
}
