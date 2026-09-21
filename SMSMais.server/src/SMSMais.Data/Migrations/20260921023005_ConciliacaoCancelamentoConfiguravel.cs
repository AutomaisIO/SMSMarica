using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConciliacaoCancelamentoConfiguravel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "conciliacao_hora_fechamento",
                schema: "smsmarica",
                table: "confirmacao_configuracao",
                type: "integer",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<int>(
                name: "conciliacao_hora_fim",
                schema: "smsmarica",
                table: "confirmacao_configuracao",
                type: "integer",
                nullable: false,
                defaultValue: 18);

            migrationBuilder.AddColumn<int>(
                name: "conciliacao_hora_inicio",
                schema: "smsmarica",
                table: "confirmacao_configuracao",
                type: "integer",
                nullable: false,
                defaultValue: 8);

            migrationBuilder.AddColumn<int>(
                name: "conciliacao_intervalo_minutos",
                schema: "smsmarica",
                table: "confirmacao_configuracao",
                type: "integer",
                nullable: false,
                defaultValue: 10);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "conciliacao_hora_fechamento",
                schema: "smsmarica",
                table: "confirmacao_configuracao");

            migrationBuilder.DropColumn(
                name: "conciliacao_hora_fim",
                schema: "smsmarica",
                table: "confirmacao_configuracao");

            migrationBuilder.DropColumn(
                name: "conciliacao_hora_inicio",
                schema: "smsmarica",
                table: "confirmacao_configuracao");

            migrationBuilder.DropColumn(
                name: "conciliacao_intervalo_minutos",
                schema: "smsmarica",
                table: "confirmacao_configuracao");
        }
    }
}
