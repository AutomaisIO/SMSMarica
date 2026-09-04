using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class EscalasSisreg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sisreg_escala",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_escala = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cnes = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    unidade_nome_sisreg = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    profissional_cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    profissional_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cbo_codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    cbo_descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    procedimento_codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    procedimento_nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    procedimento_sigtap = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    eh_grupo = table.Column<bool>(type: "boolean", nullable: false),
                    dia_semana = table.Column<int>(type: "integer", nullable: false),
                    hora_inicio = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    hora_fim = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    vigencia_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    vigencia_fim = table.Column<DateOnly>(type: "date", nullable: false),
                    vagas_primeira_vez = table.Column<int>(type: "integer", nullable: false),
                    minutos_primeira_vez = table.Column<int>(type: "integer", nullable: false),
                    vagas_retorno = table.Column<int>(type: "integer", nullable: false),
                    minutos_retorno = table.Column<int>(type: "integer", nullable: false),
                    vagas_reserva = table.Column<int>(type: "integer", nullable: false),
                    minutos_reserva = table.Column<int>(type: "integer", nullable: false),
                    vagas_total = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    agenda_local = table.Column<bool>(type: "boolean", nullable: false),
                    quebra_automatica = table.Column<bool>(type: "boolean", nullable: false),
                    operador_criador = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    operador_modificador = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    inserida_em_sisreg = table.Column<DateOnly>(type: "date", nullable: true),
                    alterada_em_sisreg = table.Column<DateOnly>(type: "date", nullable: true),
                    ativada_em_sisreg = table.Column<DateOnly>(type: "date", nullable: true),
                    visto_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ausente = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sisreg_escala", x => x.id);
                    table.ForeignKey(
                        name: "FK_sisreg_escala_unidade_unidade_id",
                        column: x => x.unidade_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sisreg_escala_profissional_cpf_vigencia_fim",
                schema: "smsmarica",
                table: "sisreg_escala",
                columns: new[] { "profissional_cpf", "vigencia_fim" });

            migrationBuilder.CreateIndex(
                name: "IX_sisreg_escala_unidade_id_procedimento_codigo_dia_semana",
                schema: "smsmarica",
                table: "sisreg_escala",
                columns: new[] { "unidade_id", "procedimento_codigo", "dia_semana" });

            migrationBuilder.CreateIndex(
                name: "IX_sisreg_escala_unidade_id_status_vigencia_fim",
                schema: "smsmarica",
                table: "sisreg_escala",
                columns: new[] { "unidade_id", "status", "vigencia_fim" });

            migrationBuilder.CreateIndex(
                name: "ux_sisreg_escala_codigo",
                schema: "smsmarica",
                table: "sisreg_escala",
                column: "codigo_escala",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sisreg_escala",
                schema: "smsmarica");
        }
    }
}
