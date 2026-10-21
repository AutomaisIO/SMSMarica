using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class GuiasAgendamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "agendamento_id_esus",
                schema: "smsmarica",
                table: "esussg_solicitacao",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "guia_agendamento",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sistema = table.Column<int>(type: "integer", nullable: false),
                    agendamento_externo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    solicitacao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_atendimento = table.Column<DateOnly>(type: "date", nullable: false),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    documento_paciente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    ultima_tentativa_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    proxima_tentativa_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ultimo_erro = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    obtida_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guia_agendamento", x => x.id);
                    table.CheckConstraint("ck_guia_agendamento_uma_ancora", "(agendamento_externo_id IS NOT NULL)::int + (solicitacao_id IS NOT NULL)::int = 1");
                    table.ForeignKey(
                        name: "FK_guia_agendamento_agendamento_externo_agendamento_externo_id",
                        column: x => x.agendamento_externo_id,
                        principalSchema: "smsmarica",
                        principalTable: "agendamento_externo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_guia_agendamento_documento_paciente_documento_paciente_id",
                        column: x => x.documento_paciente_id,
                        principalSchema: "smsmarica",
                        principalTable: "documento_paciente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_guia_agendamento_solicitacao_solicitacao_id",
                        column: x => x.solicitacao_id,
                        principalSchema: "smsmarica",
                        principalTable: "solicitacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "guia_fila_configuracao",
                schema: "smsmarica",
                columns: table => new
                {
                    sistema = table.Column<int>(type: "integer", nullable: false),
                    habilitada = table.Column<bool>(type: "boolean", nullable: false),
                    por_hora = table.Column<int>(type: "integer", nullable: false),
                    janela_inicio = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    janela_fim = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    pausada_ate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    motivo_pausa = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guia_fila_configuracao", x => x.sistema);
                    table.CheckConstraint("ck_guia_fila_configuracao_por_hora", "por_hora BETWEEN 1 AND 3600");
                });

            migrationBuilder.CreateIndex(
                name: "IX_guia_agendamento_agendamento_externo_id",
                schema: "smsmarica",
                table: "guia_agendamento",
                column: "agendamento_externo_id",
                unique: true,
                filter: "agendamento_externo_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_guia_agendamento_documento_paciente_id",
                schema: "smsmarica",
                table: "guia_agendamento",
                column: "documento_paciente_id");

            migrationBuilder.CreateIndex(
                name: "IX_guia_agendamento_paciente_id",
                schema: "smsmarica",
                table: "guia_agendamento",
                column: "paciente_id");

            migrationBuilder.CreateIndex(
                name: "IX_guia_agendamento_sistema_situacao_data_atendimento",
                schema: "smsmarica",
                table: "guia_agendamento",
                columns: new[] { "sistema", "situacao", "data_atendimento" });

            migrationBuilder.CreateIndex(
                name: "IX_guia_agendamento_solicitacao_id_data_atendimento",
                schema: "smsmarica",
                table: "guia_agendamento",
                columns: new[] { "solicitacao_id", "data_atendimento" },
                unique: true,
                filter: "solicitacao_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guia_agendamento",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "guia_fila_configuracao",
                schema: "smsmarica");

            migrationBuilder.DropColumn(
                name: "agendamento_id_esus",
                schema: "smsmarica",
                table: "esussg_solicitacao");
        }
    }
}
