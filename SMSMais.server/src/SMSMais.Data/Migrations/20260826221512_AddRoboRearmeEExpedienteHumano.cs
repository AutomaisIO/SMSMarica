using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoboRearmeEExpedienteHumano : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "hora_atendimento_humano_fim",
                schema: "smsmarica",
                table: "robo_configuracao",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "hora_atendimento_humano_inicio",
                schema: "smsmarica",
                table: "robo_configuracao",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "robo_rearmado_em",
                schema: "smsmarica",
                table: "conversa",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "hora_atendimento_humano_fim",
                schema: "smsmarica",
                table: "robo_configuracao");

            migrationBuilder.DropColumn(
                name: "hora_atendimento_humano_inicio",
                schema: "smsmarica",
                table: "robo_configuracao");

            migrationBuilder.DropColumn(
                name: "robo_rearmado_em",
                schema: "smsmarica",
                table: "conversa");
        }
    }
}
