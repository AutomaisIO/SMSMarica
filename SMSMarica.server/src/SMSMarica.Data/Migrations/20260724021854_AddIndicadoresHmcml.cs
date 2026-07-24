using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIndicadoresHmcml : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "indicador",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aba = table.Column<int>(type: "integer", nullable: false),
                    numero = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    indicador_pai_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nome = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    memoria_calculo = table.Column<string>(type: "text", nullable: true),
                    fonte_declarada = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    meta = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    meta_operador = table.Column<int>(type: "integer", nullable: true),
                    meta_valor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    meta_valor_maximo = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    pontuacao = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    tipo_resultado = table.Column<int>(type: "integer", nullable: false),
                    unidade_medida = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    fator_densidade = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    fonte_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sql = table.Column<string>(type: "text", nullable: true),
                    ressalva = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_indicador", x => x.id);
                    table.ForeignKey(
                        name: "FK_indicador_ia_fonte_fonte_id",
                        column: x => x.fonte_id,
                        principalSchema: "smsmarica",
                        principalTable: "ia_fonte",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_indicador_indicador_indicador_pai_id",
                        column: x => x.indicador_pai_id,
                        principalSchema: "smsmarica",
                        principalTable: "indicador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "indicador_execucao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    indicador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    indicador_versao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    hospital = table.Column<int>(type: "integer", nullable: false),
                    periodo_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    periodo_fim = table.Column<DateOnly>(type: "date", nullable: false),
                    numerador = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    denominador = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    valor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    distribuicao_json = table.Column<string>(type: "jsonb", nullable: true),
                    atingiu_meta = table.Column<bool>(type: "boolean", nullable: true),
                    pontuacao_apurada = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    duracao_ms = table.Column<int>(type: "integer", nullable: false),
                    erro = table.Column<string>(type: "text", nullable: true),
                    executado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    executado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_indicador_execucao", x => x.id);
                    table.ForeignKey(
                        name: "FK_indicador_execucao_indicador_indicador_id",
                        column: x => x.indicador_id,
                        principalSchema: "smsmarica",
                        principalTable: "indicador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "indicador_versao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    indicador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    sql = table.Column<string>(type: "text", nullable: false),
                    nota = table.Column<string>(type: "text", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_indicador_versao", x => x.id);
                    table.ForeignKey(
                        name: "FK_indicador_versao_indicador_indicador_id",
                        column: x => x.indicador_id,
                        principalSchema: "smsmarica",
                        principalTable: "indicador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_indicador_excluido_em",
                schema: "smsmarica",
                table: "indicador",
                column: "excluido_em",
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_indicador_fonte_id",
                schema: "smsmarica",
                table: "indicador",
                column: "fonte_id");

            migrationBuilder.CreateIndex(
                name: "IX_indicador_indicador_pai_id",
                schema: "smsmarica",
                table: "indicador",
                column: "indicador_pai_id");

            migrationBuilder.CreateIndex(
                name: "ux_indicador_aba_numero",
                schema: "smsmarica",
                table: "indicador",
                columns: new[] { "aba", "numero" },
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_indicador_execucao_periodo",
                schema: "smsmarica",
                table: "indicador_execucao",
                columns: new[] { "indicador_id", "hospital", "periodo_inicio", "periodo_fim" });

            migrationBuilder.CreateIndex(
                name: "ux_indicador_versao_num",
                schema: "smsmarica",
                table: "indicador_versao",
                columns: new[] { "indicador_id", "numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "indicador_execucao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "indicador_versao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "indicador",
                schema: "smsmarica");
        }
    }
}
