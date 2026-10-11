using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AvisoAgendamentoExterno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "aviso_agendamento_externo_habilitado",
                schema: "smsmarica",
                table: "confirmacao_configuracao",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "agendamento_externo_id",
                schema: "smsmarica",
                table: "comunicacao_paciente",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "agendamento_externo",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sistema = table.Column<int>(type: "integer", nullable: false),
                    origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_externo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    procedimento = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    hora = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    local_nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    encerrado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status_confirmacao = table.Column<int>(type: "integer", nullable: false),
                    confirmado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    confirmado_canal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    confirmacao_cancelada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    motivo_cancelamento_paciente = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agendamento_externo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "unidade_atendimento_nome_externo",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_atendimento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    nome_normalizado = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unidade_atendimento_nome_externo", x => x.id);
                    table.ForeignKey(
                        name: "FK_unidade_atendimento_nome_externo_unidade_atendimento_unidad~",
                        column: x => x.unidade_atendimento_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade_atendimento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_comunicacao_paciente_agendamento_externo_id_finalidade",
                schema: "smsmarica",
                table: "comunicacao_paciente",
                columns: new[] { "agendamento_externo_id", "finalidade" },
                unique: true,
                filter: "agendamento_externo_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_agendamento_externo_origem_id",
                schema: "smsmarica",
                table: "agendamento_externo",
                column: "origem_id");

            migrationBuilder.CreateIndex(
                name: "IX_agendamento_externo_paciente_id",
                schema: "smsmarica",
                table: "agendamento_externo",
                column: "paciente_id");

            migrationBuilder.CreateIndex(
                name: "IX_agendamento_externo_sistema_origem_id",
                schema: "smsmarica",
                table: "agendamento_externo",
                columns: new[] { "sistema", "origem_id" },
                unique: true,
                filter: "situacao = 1");

            migrationBuilder.CreateIndex(
                name: "IX_unidade_atendimento_nome_externo_nome_normalizado",
                schema: "smsmarica",
                table: "unidade_atendimento_nome_externo",
                column: "nome_normalizado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_unidade_atendimento_nome_externo_unidade_atendimento_id",
                schema: "smsmarica",
                table: "unidade_atendimento_nome_externo",
                column: "unidade_atendimento_id");

            migrationBuilder.AddForeignKey(
                name: "FK_comunicacao_paciente_agendamento_externo_agendamento_extern~",
                schema: "smsmarica",
                table: "comunicacao_paciente",
                column: "agendamento_externo_id",
                principalSchema: "smsmarica",
                principalTable: "agendamento_externo",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_comunicacao_paciente_agendamento_externo_agendamento_extern~",
                schema: "smsmarica",
                table: "comunicacao_paciente");

            migrationBuilder.DropTable(
                name: "agendamento_externo",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "unidade_atendimento_nome_externo",
                schema: "smsmarica");

            migrationBuilder.DropIndex(
                name: "IX_comunicacao_paciente_agendamento_externo_id_finalidade",
                schema: "smsmarica",
                table: "comunicacao_paciente");

            migrationBuilder.DropColumn(
                name: "aviso_agendamento_externo_habilitado",
                schema: "smsmarica",
                table: "confirmacao_configuracao");

            migrationBuilder.DropColumn(
                name: "agendamento_externo_id",
                schema: "smsmarica",
                table: "comunicacao_paciente");
        }
    }
}
