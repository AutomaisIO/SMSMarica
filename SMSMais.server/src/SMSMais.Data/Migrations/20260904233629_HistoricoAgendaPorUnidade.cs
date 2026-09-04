using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class HistoricoAgendaPorUnidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "historico_ativo",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "historico_coberto_de",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "historico_concluido_em",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "historico_fatias_vazias",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "historico_ativo",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda");

            migrationBuilder.DropColumn(
                name: "historico_coberto_de",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda");

            migrationBuilder.DropColumn(
                name: "historico_concluido_em",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda");

            migrationBuilder.DropColumn(
                name: "historico_fatias_vazias",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda");
        }
    }
}
