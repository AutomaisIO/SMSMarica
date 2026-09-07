using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class MigracaoRascunhosLegados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "rascunhos_legados_migrados_em",
                schema: "smsmarica",
                table: "regulacao_configuracao",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_regulacao_solicitacao_origem_legado",
                schema: "smsmarica",
                table: "regulacao_solicitacao",
                column: "origem_legado_id",
                unique: true,
                filter: "origem_legado_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_regulacao_solicitacao_origem_legado",
                schema: "smsmarica",
                table: "regulacao_solicitacao");

            migrationBuilder.DropColumn(
                name: "rascunhos_legados_migrados_em",
                schema: "smsmarica",
                table: "regulacao_configuracao");
        }
    }
}
