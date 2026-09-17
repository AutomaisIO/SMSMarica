using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConfirmacoesJanelaHorario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ignorar_janela_horario",
                schema: "smsmarica",
                table: "comunicacao_paciente",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "confirmacao_configuracao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hora_inicio_envio = table.Column<TimeOnly>(type: "time without time zone", nullable: false, defaultValue: new TimeOnly(8, 0, 0)),
                    hora_fim_envio = table.Column<TimeOnly>(type: "time without time zone", nullable: false, defaultValue: new TimeOnly(18, 0, 0)),
                    maximo_por_passagem = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    somente_sisreg = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_confirmacao_configuracao", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "confirmacao_configuracao",
                schema: "smsmarica");

            migrationBuilder.DropColumn(
                name: "ignorar_janela_horario",
                schema: "smsmarica",
                table: "comunicacao_paciente");
        }
    }
}
