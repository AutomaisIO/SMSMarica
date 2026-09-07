using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class RegrasDeElegibilidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "regulacao_regra",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    procedimento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    procedimento_origem_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sistema = table.Column<int>(type: "integer", nullable: true),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    severidade = table.Column<int>(type: "integer", nullable: false),
                    descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    fonte = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    idade_min_anos = table.Column<int>(type: "integer", nullable: true),
                    idade_max_anos = table.Column<int>(type: "integer", nullable: true),
                    sexo = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    exige_cpf = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    cids_permitidos_json = table.Column<string>(type: "jsonb", nullable: true),
                    cids_excluidos_json = table.Column<string>(type: "jsonb", nullable: true),
                    municipios_ibge_json = table.Column<string>(type: "jsonb", nullable: true),
                    expressao_json = table.Column<string>(type: "jsonb", nullable: true),
                    pergunta = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    resposta_bloqueia = table.Column<int>(type: "integer", nullable: true),
                    nao_sei_vira = table.Column<int>(type: "integer", nullable: true),
                    documento_rotulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    tipo_exame_id = table.Column<Guid>(type: "uuid", nullable: true),
                    validade_dias = table.Column<int>(type: "integer", nullable: true),
                    obrigatorio = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ordem = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    versao = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulacao_regra", x => x.id);
                    table.ForeignKey(
                        name: "FK_regulacao_regra_regulacao_procedimento_origem_procedimento_~",
                        column: x => x.procedimento_origem_id,
                        principalSchema: "smsmarica",
                        principalTable: "regulacao_procedimento_origem",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_regulacao_regra_regulacao_procedimento_procedimento_id",
                        column: x => x.procedimento_id,
                        principalSchema: "smsmarica",
                        principalTable: "regulacao_procedimento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "regulacao_solicitacao_resposta_regra",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    regra_id = table.Column<Guid>(type: "uuid", nullable: false),
                    regra_versao = table.Column<int>(type: "integer", nullable: false),
                    resposta = table.Column<int>(type: "integer", nullable: false),
                    valor_deduzido = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    resultado = table.Column<int>(type: "integer", nullable: false),
                    respondido_por = table.Column<Guid>(type: "uuid", nullable: true),
                    respondido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulacao_solicitacao_resposta_regra", x => x.id);
                    table.ForeignKey(
                        name: "FK_regulacao_solicitacao_resposta_regra_regulacao_regra_regra_~",
                        column: x => x.regra_id,
                        principalSchema: "smsmarica",
                        principalTable: "regulacao_regra",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_regulacao_solicitacao_resposta_regra_regulacao_solicitacao_~",
                        column: x => x.solicitacao_id,
                        principalSchema: "smsmarica",
                        principalTable: "regulacao_solicitacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_regulacao_regra_proc_sistema_ativo",
                schema: "smsmarica",
                table: "regulacao_regra",
                columns: new[] { "procedimento_id", "sistema", "ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_regulacao_regra_procedimento_origem_id",
                schema: "smsmarica",
                table: "regulacao_regra",
                column: "procedimento_origem_id");

            migrationBuilder.CreateIndex(
                name: "IX_regulacao_solicitacao_resposta_regra_regra_id",
                schema: "smsmarica",
                table: "regulacao_solicitacao_resposta_regra",
                column: "regra_id");

            migrationBuilder.CreateIndex(
                name: "ux_regulacao_resposta",
                schema: "smsmarica",
                table: "regulacao_solicitacao_resposta_regra",
                columns: new[] { "solicitacao_id", "regra_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "regulacao_solicitacao_resposta_regra",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "regulacao_regra",
                schema: "smsmarica");
        }
    }
}
