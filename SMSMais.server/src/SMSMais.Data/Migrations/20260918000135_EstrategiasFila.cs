using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class EstrategiasFila : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "estrategia_fila",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    procedimento_codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    procedimento_nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    regulacao_procedimento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    parametros_json = table.Column<string>(type: "jsonb", nullable: false),
                    rodada_atual_id = table.Column<Guid>(type: "uuid", nullable: true),
                    aplicada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    aplicada_por = table.Column<Guid>(type: "uuid", nullable: true),
                    aplicacao_nota = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_estrategia_fila", x => x.id);
                    table.ForeignKey(
                        name: "FK_estrategia_fila_regulacao_procedimento_regulacao_procedimen~",
                        column: x => x.regulacao_procedimento_id,
                        principalSchema: "smsmarica",
                        principalTable: "regulacao_procedimento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "estrategia_fila_rodada",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    estrategia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    modo = table.Column<int>(type: "integer", nullable: false),
                    parametros_entrada_json = table.Column<string>(type: "jsonb", nullable: false),
                    cenario_json = table.Column<string>(type: "jsonb", nullable: false),
                    parametros_resultado_json = table.Column<string>(type: "jsonb", nullable: false),
                    projecao_json = table.Column<string>(type: "jsonb", nullable: false),
                    proposta_json = table.Column<string>(type: "jsonb", nullable: true),
                    modelo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    tokens_entrada = table.Column<long>(type: "bigint", nullable: false),
                    tokens_saida = table.Column<long>(type: "bigint", nullable: false),
                    custo_usd = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: true),
                    duracao_ms = table.Column<int>(type: "integer", nullable: false),
                    falha = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_estrategia_fila_rodada", x => x.id);
                    table.ForeignKey(
                        name: "FK_estrategia_fila_rodada_estrategia_fila_estrategia_id",
                        column: x => x.estrategia_id,
                        principalSchema: "smsmarica",
                        principalTable: "estrategia_fila",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_estrategia_fila_procedimento_codigo",
                schema: "smsmarica",
                table: "estrategia_fila",
                column: "procedimento_codigo");

            migrationBuilder.CreateIndex(
                name: "ix_estrategia_fila_procedimento_nome",
                schema: "smsmarica",
                table: "estrategia_fila",
                column: "procedimento_nome");

            migrationBuilder.CreateIndex(
                name: "IX_estrategia_fila_regulacao_procedimento_id",
                schema: "smsmarica",
                table: "estrategia_fila",
                column: "regulacao_procedimento_id");

            migrationBuilder.CreateIndex(
                name: "ix_estrategia_fila_status_viva",
                schema: "smsmarica",
                table: "estrategia_fila",
                column: "status",
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_estrategia_fila_rodada_numero",
                schema: "smsmarica",
                table: "estrategia_fila_rodada",
                columns: new[] { "estrategia_id", "numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "estrategia_fila_rodada",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "estrategia_fila",
                schema: "smsmarica");
        }
    }
}
