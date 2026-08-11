using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class GateCpfMagicLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "exige_confirmacao_cpf",
                schema: "smsmarica",
                table: "cidadao_login_link",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "tentativas_cpf",
                schema: "smsmarica",
                table: "cidadao_login_link",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "exige_confirmacao_cpf",
                schema: "smsmarica",
                table: "cidadao_login_link");

            migrationBuilder.DropColumn(
                name: "tentativas_cpf",
                schema: "smsmarica",
                table: "cidadao_login_link");
        }
    }
}
