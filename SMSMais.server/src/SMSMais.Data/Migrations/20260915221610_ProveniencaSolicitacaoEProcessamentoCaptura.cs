using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProveniencaSolicitacaoEProcessamentoCaptura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "fonte_criacao",
                schema: "smsmarica",
                table: "solicitacao",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "processado_em",
                schema: "smsmarica",
                table: "sisreg_captura_navegador",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fonte_criacao",
                schema: "smsmarica",
                table: "solicitacao");

            migrationBuilder.DropColumn(
                name: "processado_em",
                schema: "smsmarica",
                table: "sisreg_captura_navegador");
        }
    }
}
