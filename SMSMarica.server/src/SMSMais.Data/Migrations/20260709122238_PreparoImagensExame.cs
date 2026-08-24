using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class PreparoImagensExame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "imagens_preparacao_tentativas",
                schema: "smsmarica",
                table: "solicitacao_exame",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "imagens_preparadas_em",
                schema: "smsmarica",
                table: "solicitacao_exame",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "imagens_preparacao_tentativas",
                schema: "smsmarica",
                table: "solicitacao_exame");

            migrationBuilder.DropColumn(
                name: "imagens_preparadas_em",
                schema: "smsmarica",
                table: "solicitacao_exame");
        }
    }
}
