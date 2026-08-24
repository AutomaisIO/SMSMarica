using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketLeituraNotificacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "respondido_em",
                schema: "smsmarica",
                table: "ticket",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "resposta_reconhecida_em",
                schema: "smsmarica",
                table: "ticket",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "visto_pela_gestao_em",
                schema: "smsmarica",
                table: "ticket",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "respondido_em",
                schema: "smsmarica",
                table: "ticket");

            migrationBuilder.DropColumn(
                name: "resposta_reconhecida_em",
                schema: "smsmarica",
                table: "ticket");

            migrationBuilder.DropColumn(
                name: "visto_pela_gestao_em",
                schema: "smsmarica",
                table: "ticket");
        }
    }
}
