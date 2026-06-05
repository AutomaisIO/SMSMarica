using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgendamentoESisreg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "especialidade",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    codigo_cbo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_especialidade", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sisreg_configuracao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_url = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    escopo = table.Column<int>(type: "integer", nullable: false),
                    uf = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    municipio = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    centrais_reguladoras = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    tipo_autenticacao = table.Column<int>(type: "integer", nullable: false),
                    login = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    senha_cifrada = table.Column<string>(type: "text", nullable: true),
                    token_cifrado = table.Column<string>(type: "text", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sisreg_configuracao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agenda",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    especialidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    medico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    medico_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    medico_cns = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    duracao_consulta_minutos = table.Column<int>(type: "integer", nullable: false),
                    vigencia_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    vigencia_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agenda", x => x.id);
                    table.ForeignKey(
                        name: "FK_agenda_especialidade_especialidade_id",
                        column: x => x.especialidade_id,
                        principalSchema: "smsmarica",
                        principalTable: "especialidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_agenda_unidade_unidade_id",
                        column: x => x.unidade_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agendamento",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agenda_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paciente_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    paciente_cns = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    inicio_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    fim_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    confirmado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    realizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    motivo_cancelamento = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agendamento", x => x.id);
                    table.ForeignKey(
                        name: "FK_agendamento_agenda_agenda_id",
                        column: x => x.agenda_id,
                        principalSchema: "smsmarica",
                        principalTable: "agenda",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bloqueio_agenda",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agenda_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inicio_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    fim_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bloqueio_agenda", x => x.id);
                    table.ForeignKey(
                        name: "FK_bloqueio_agenda_agenda_agenda_id",
                        column: x => x.agenda_id,
                        principalSchema: "smsmarica",
                        principalTable: "agenda",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "disponibilidade_avulsa",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agenda_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inicio_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    fim_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_disponibilidade_avulsa", x => x.id);
                    table.ForeignKey(
                        name: "FK_disponibilidade_avulsa_agenda_agenda_id",
                        column: x => x.agenda_id,
                        principalSchema: "smsmarica",
                        principalTable: "agenda",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "disponibilidade_recorrente",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agenda_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dia_semana = table.Column<int>(type: "integer", nullable: false),
                    hora_inicio = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    hora_fim = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    vigencia_inicio = table.Column<DateOnly>(type: "date", nullable: true),
                    vigencia_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_disponibilidade_recorrente", x => x.id);
                    table.ForeignKey(
                        name: "FK_disponibilidade_recorrente_agenda_agenda_id",
                        column: x => x.agenda_id,
                        principalSchema: "smsmarica",
                        principalTable: "agenda",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agenda_especialidade_id",
                schema: "smsmarica",
                table: "agenda",
                column: "especialidade_id");

            migrationBuilder.CreateIndex(
                name: "ix_agenda_excluido_em",
                schema: "smsmarica",
                table: "agenda",
                column: "excluido_em",
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_agenda_medico_id",
                schema: "smsmarica",
                table: "agenda",
                column: "medico_id");

            migrationBuilder.CreateIndex(
                name: "ix_agenda_unidade_especialidade",
                schema: "smsmarica",
                table: "agenda",
                columns: new[] { "unidade_id", "especialidade_id" });

            migrationBuilder.CreateIndex(
                name: "ix_agendamento_agenda_inicio",
                schema: "smsmarica",
                table: "agendamento",
                columns: new[] { "agenda_id", "inicio_em" });

            migrationBuilder.CreateIndex(
                name: "ix_agendamento_excluido_em",
                schema: "smsmarica",
                table: "agendamento",
                column: "excluido_em",
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_agendamento_paciente_id",
                schema: "smsmarica",
                table: "agendamento",
                column: "paciente_id");

            migrationBuilder.CreateIndex(
                name: "ix_bloqueio_agenda_agenda_id",
                schema: "smsmarica",
                table: "bloqueio_agenda",
                column: "agenda_id");

            migrationBuilder.CreateIndex(
                name: "ix_disponibilidade_avulsa_agenda_id",
                schema: "smsmarica",
                table: "disponibilidade_avulsa",
                column: "agenda_id");

            migrationBuilder.CreateIndex(
                name: "ix_disponibilidade_recorrente_agenda_id",
                schema: "smsmarica",
                table: "disponibilidade_recorrente",
                column: "agenda_id");

            migrationBuilder.CreateIndex(
                name: "ix_especialidade_excluido_em",
                schema: "smsmarica",
                table: "especialidade",
                column: "excluido_em",
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_especialidade_nome",
                schema: "smsmarica",
                table: "especialidade",
                column: "nome",
                unique: true,
                filter: "excluido_em IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agendamento",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "bloqueio_agenda",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "disponibilidade_avulsa",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "disponibilidade_recorrente",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "sisreg_configuracao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "agenda",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "especialidade",
                schema: "smsmarica");
        }
    }
}
