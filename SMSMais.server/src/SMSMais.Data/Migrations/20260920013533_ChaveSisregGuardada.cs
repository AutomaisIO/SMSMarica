using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChaveSisregGuardada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "chave_confirmacao_sisreg",
                schema: "smsmarica",
                table: "solicitacao",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "chave_sisreg_lida_em",
                schema: "smsmarica",
                table: "solicitacao",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "chave_confirmacao_sisreg",
                schema: "smsmarica",
                table: "solicitacao");

            migrationBuilder.DropColumn(
                name: "chave_sisreg_lida_em",
                schema: "smsmarica",
                table: "solicitacao");
        }
    }
}
