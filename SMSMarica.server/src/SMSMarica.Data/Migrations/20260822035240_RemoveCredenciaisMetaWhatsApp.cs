using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCredenciaisMetaWhatsApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "app_secret_cifrado",
                schema: "smsmarica",
                table: "whatsapp_configuracao");

            migrationBuilder.DropColumn(
                name: "base_url",
                schema: "smsmarica",
                table: "whatsapp_configuracao");

            migrationBuilder.DropColumn(
                name: "token_cifrado",
                schema: "smsmarica",
                table: "whatsapp_configuracao");

            migrationBuilder.DropColumn(
                name: "verify_token_cifrado",
                schema: "smsmarica",
                table: "whatsapp_configuracao");

            migrationBuilder.DropColumn(
                name: "waba_id",
                schema: "smsmarica",
                table: "whatsapp_configuracao");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "app_secret_cifrado",
                schema: "smsmarica",
                table: "whatsapp_configuracao",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "base_url",
                schema: "smsmarica",
                table: "whatsapp_configuracao",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "token_cifrado",
                schema: "smsmarica",
                table: "whatsapp_configuracao",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "verify_token_cifrado",
                schema: "smsmarica",
                table: "whatsapp_configuracao",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "waba_id",
                schema: "smsmarica",
                table: "whatsapp_configuracao",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);
        }
    }
}
