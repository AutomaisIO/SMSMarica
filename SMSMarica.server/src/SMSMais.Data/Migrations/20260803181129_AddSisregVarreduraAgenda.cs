using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSisregVarreduraAgenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sisreg_procedimento_sigtap",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    grupo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    procedimento_sigtap_id = table.Column<Guid>(type: "uuid", nullable: true),
                    codigo_sigtap = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    sugerido_sigtap_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sugerido_score = table.Column<float>(type: "real", nullable: true),
                    confirmado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    confirmado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    primeiro_visto_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    visto_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sisreg_procedimento_sigtap", x => x.id);
                    table.ForeignKey(
                        name: "FK_sisreg_procedimento_sigtap_procedimento_sigtap_procedimento~",
                        column: x => x.procedimento_sigtap_id,
                        principalSchema: "smsmarica",
                        principalTable: "procedimento_sigtap",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "sisreg_varredura_agenda",
                schema: "smsmarica",
                columns: table => new
                {
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    hora_local = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    dias_a_frente = table.Column<int>(type: "integer", nullable: false, defaultValue: 21),
                    falhas_consecutivas = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    proximo_run_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    pausado_ate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ultima_execucao_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ultima_execucao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cursor_profissional_cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    cursor_procedimento_codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    cursor_janela_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sisreg_varredura_agenda", x => x.unidade_id);
                    table.ForeignKey(
                        name: "FK_sisreg_varredura_agenda_unidade_unidade_id",
                        column: x => x.unidade_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sisreg_varredura_execucao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    disparo = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    status = table.Column<int>(type: "integer", nullable: false),
                    janela_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    janela_fim = table.Column<DateOnly>(type: "date", nullable: false),
                    combinacoes_total = table.Column<int>(type: "integer", nullable: false),
                    combinacoes_feitas = table.Column<int>(type: "integer", nullable: false),
                    requisicoes = table.Column<int>(type: "integer", nullable: false),
                    registros_encontrados = table.Column<int>(type: "integer", nullable: false),
                    validos = table.Column<int>(type: "integer", nullable: false),
                    invalidos = table.Column<int>(type: "integer", nullable: false),
                    ja_existiam = table.Column<int>(type: "integer", nullable: false),
                    cursor_profissional_cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    cursor_procedimento_codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    mensagem_erro = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    iniciado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finalizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    duracao_segundos = table.Column<int>(type: "integer", nullable: true),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    criado_por_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sisreg_varredura_execucao", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_procedimento_sigtap_confirmado",
                schema: "smsmarica",
                table: "sisreg_procedimento_sigtap",
                column: "confirmado_em");

            migrationBuilder.CreateIndex(
                name: "IX_sisreg_procedimento_sigtap_procedimento_sigtap_id",
                schema: "smsmarica",
                table: "sisreg_procedimento_sigtap",
                column: "procedimento_sigtap_id");

            migrationBuilder.CreateIndex(
                name: "ux_sisreg_procedimento_sigtap_codigo",
                schema: "smsmarica",
                table: "sisreg_procedimento_sigtap",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_varredura_agenda_elegivel",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda",
                columns: new[] { "ativo", "proximo_run_em" });

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_varredura_execucao_unidade_iniciado",
                schema: "smsmarica",
                table: "sisreg_varredura_execucao",
                columns: new[] { "unidade_id", "iniciado_em" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sisreg_procedimento_sigtap",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "sisreg_varredura_agenda",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "sisreg_varredura_execucao",
                schema: "smsmarica");
        }
    }
}
