using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class CatalogoCanonicoDeProcedimentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "regulacao_procedimento",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_canonico = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    nome_normalizado = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    procedimento_sigtap_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulacao_procedimento", x => x.id);
                    table.ForeignKey(
                        name: "FK_regulacao_procedimento_procedimento_sigtap_procedimento_sig~",
                        column: x => x.procedimento_sigtap_id,
                        principalSchema: "smsmarica",
                        principalTable: "procedimento_sigtap",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "regulacao_procedimento_origem",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    procedimento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sistema = table.Column<int>(type: "integer", nullable: false),
                    chave_externa = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    rotulo_externo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ramo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    sisreg_procedimento_sigtap_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ser_catalogo_recurso_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sernit_catalogo_recurso_id = table.Column<Guid>(type: "uuid", nullable: true),
                    embedding = table.Column<Vector>(type: "vector(1024)", nullable: true),
                    embedding_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    embedding_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    vinculo = table.Column<int>(type: "integer", nullable: false),
                    sugerido_procedimento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sugerido_score = table.Column<double>(type: "double precision", nullable: true),
                    confirmado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    confirmado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulacao_procedimento_origem", x => x.id);
                    table.ForeignKey(
                        name: "FK_regulacao_procedimento_origem_regulacao_procedimento_proced~",
                        column: x => x.procedimento_id,
                        principalSchema: "smsmarica",
                        principalTable: "regulacao_procedimento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_regulacao_procedimento_origem_ser_catalogo_recurso_ser_cata~",
                        column: x => x.ser_catalogo_recurso_id,
                        principalSchema: "smsmarica",
                        principalTable: "ser_catalogo_recurso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_regulacao_procedimento_origem_sernit_catalogo_recurso_serni~",
                        column: x => x.sernit_catalogo_recurso_id,
                        principalSchema: "smsmarica",
                        principalTable: "sernit_catalogo_recurso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_regulacao_procedimento_origem_sisreg_procedimento_sigtap_si~",
                        column: x => x.sisreg_procedimento_sigtap_id,
                        principalSchema: "smsmarica",
                        principalTable: "sisreg_procedimento_sigtap",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_regulacao_procedimento_nome_normalizado",
                schema: "smsmarica",
                table: "regulacao_procedimento",
                column: "nome_normalizado");

            migrationBuilder.CreateIndex(
                name: "IX_regulacao_procedimento_procedimento_sigtap_id",
                schema: "smsmarica",
                table: "regulacao_procedimento",
                column: "procedimento_sigtap_id");

            migrationBuilder.CreateIndex(
                name: "ix_regulacao_proc_origem_procedimento",
                schema: "smsmarica",
                table: "regulacao_procedimento_origem",
                column: "procedimento_id");

            migrationBuilder.CreateIndex(
                name: "ix_regulacao_proc_origem_sistema_ativo",
                schema: "smsmarica",
                table: "regulacao_procedimento_origem",
                columns: new[] { "sistema", "ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_regulacao_procedimento_origem_ser_catalogo_recurso_id",
                schema: "smsmarica",
                table: "regulacao_procedimento_origem",
                column: "ser_catalogo_recurso_id");

            migrationBuilder.CreateIndex(
                name: "IX_regulacao_procedimento_origem_sernit_catalogo_recurso_id",
                schema: "smsmarica",
                table: "regulacao_procedimento_origem",
                column: "sernit_catalogo_recurso_id");

            migrationBuilder.CreateIndex(
                name: "IX_regulacao_procedimento_origem_sisreg_procedimento_sigtap_id",
                schema: "smsmarica",
                table: "regulacao_procedimento_origem",
                column: "sisreg_procedimento_sigtap_id");

            migrationBuilder.CreateIndex(
                name: "ux_regulacao_proc_origem_sistema_chave",
                schema: "smsmarica",
                table: "regulacao_procedimento_origem",
                columns: new[] { "sistema", "chave_externa" },
                unique: true);

            // HNSW em SQL cru: o EF nao sabe gerar indice de vetor. Sem ele a busca semantica
            // faz varredura sequencial em toda a tabela de origens a cada tecla digitada.
            // `vector_cosine_ops` porque a busca compara por distancia de cosseno.
            migrationBuilder.Sql(
                "CREATE INDEX ix_regulacao_proc_origem_embedding_hnsw "
                + "ON smsmarica.regulacao_procedimento_origem "
                + "USING hnsw (embedding vector_cosine_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS smsmarica.ix_regulacao_proc_origem_embedding_hnsw;");

            migrationBuilder.DropTable(
                name: "regulacao_procedimento_origem",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "regulacao_procedimento",
                schema: "smsmarica");
        }
    }
}
