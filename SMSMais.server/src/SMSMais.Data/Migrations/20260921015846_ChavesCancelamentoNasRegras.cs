using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChavesCancelamentoNasRegras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "aviso_cancelamento_habilitado",
                schema: "smsmarica",
                table: "confirmacao_configuracao",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "conciliacao_cancelamento_habilitada",
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
                name: "aviso_cancelamento_habilitado",
                schema: "smsmarica",
                table: "confirmacao_configuracao");

            migrationBuilder.DropColumn(
                name: "conciliacao_cancelamento_habilitada",
                schema: "smsmarica",
                table: "confirmacao_configuracao");
        }
    }
}
