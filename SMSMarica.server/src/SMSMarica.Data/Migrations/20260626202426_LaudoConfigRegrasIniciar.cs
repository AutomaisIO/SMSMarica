using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class LaudoConfigRegrasIniciar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "permitir_laudar_sem_anamnese",
                schema: "smsmarica",
                table: "laudo_configuracao",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "permitir_laudar_sem_associacao",
                schema: "smsmarica",
                table: "laudo_configuracao",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "permitir_laudar_sem_anamnese",
                schema: "smsmarica",
                table: "laudo_configuracao");

            migrationBuilder.DropColumn(
                name: "permitir_laudar_sem_associacao",
                schema: "smsmarica",
                table: "laudo_configuracao");
        }
    }
}
