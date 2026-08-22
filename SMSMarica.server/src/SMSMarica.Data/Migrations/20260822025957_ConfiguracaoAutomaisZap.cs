using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConfiguracaoAutomaisZap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "zap_ativo",
                schema: "smsmarica",
                table: "whatsapp_configuracao",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "zap_base_url",
                schema: "smsmarica",
                table: "whatsapp_configuracao",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "zap_segredo_webhook_cifrado",
                schema: "smsmarica",
                table: "whatsapp_configuracao",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "zap_token_cifrado",
                schema: "smsmarica",
                table: "whatsapp_configuracao",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "zap_ativo",
                schema: "smsmarica",
                table: "whatsapp_configuracao");

            migrationBuilder.DropColumn(
                name: "zap_base_url",
                schema: "smsmarica",
                table: "whatsapp_configuracao");

            migrationBuilder.DropColumn(
                name: "zap_segredo_webhook_cifrado",
                schema: "smsmarica",
                table: "whatsapp_configuracao");

            migrationBuilder.DropColumn(
                name: "zap_token_cifrado",
                schema: "smsmarica",
                table: "whatsapp_configuracao");
        }
    }
}
