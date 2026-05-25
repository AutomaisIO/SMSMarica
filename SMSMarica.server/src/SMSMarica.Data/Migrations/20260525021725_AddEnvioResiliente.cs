using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEnvioResiliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "proxima_tentativa_em",
                schema: "smsmarica",
                table: "solicitacao_exame",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tentativas_envio",
                schema: "smsmarica",
                table: "solicitacao_exame",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ultima_tentativa_em",
                schema: "smsmarica",
                table: "solicitacao_exame",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_exame_status_proxima_tentativa_em",
                schema: "smsmarica",
                table: "solicitacao_exame",
                columns: new[] { "status", "proxima_tentativa_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_solicitacao_exame_status_proxima_tentativa_em",
                schema: "smsmarica",
                table: "solicitacao_exame");

            migrationBuilder.DropColumn(
                name: "proxima_tentativa_em",
                schema: "smsmarica",
                table: "solicitacao_exame");

            migrationBuilder.DropColumn(
                name: "tentativas_envio",
                schema: "smsmarica",
                table: "solicitacao_exame");

            migrationBuilder.DropColumn(
                name: "ultima_tentativa_em",
                schema: "smsmarica",
                table: "solicitacao_exame");
        }
    }
}
