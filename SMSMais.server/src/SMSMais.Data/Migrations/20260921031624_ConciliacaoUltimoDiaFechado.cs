using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConciliacaoUltimoDiaFechado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "conciliacao_ultimo_dia_fechado",
                schema: "smsmarica",
                table: "confirmacao_configuracao",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "conciliacao_ultimo_dia_fechado",
                schema: "smsmarica",
                table: "confirmacao_configuracao");
        }
    }
}
