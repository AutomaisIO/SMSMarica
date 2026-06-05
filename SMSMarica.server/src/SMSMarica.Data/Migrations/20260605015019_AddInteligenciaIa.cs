using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInteligenciaIa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "ia_configuracao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provedor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    token_cifrado = table.Column<string>(type: "text", nullable: true),
                    modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provedor_embeddings = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    token_embeddings_cifrado = table.Column<string>(type: "text", nullable: true),
                    modelo_embeddings = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ia_configuracao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ia_fonte",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    dialeto = table.Column<int>(type: "integer", nullable: false),
                    ambiente = table.Column<int>(type: "integer", nullable: false),
                    host = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    porta = table.Column<int>(type: "integer", nullable: true),
                    servico = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    usuario = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    senha_cifrada = table.Column<string>(type: "text", nullable: true),
                    base_url = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_ia_fonte", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ia_aprendizado",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fonte_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    origem = table.Column<int>(type: "integer", nullable: false),
                    conteudo = table.Column<string>(type: "text", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(1024)", nullable: true),
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
                    table.PrimaryKey("PK_ia_aprendizado", x => x.id);
                    table.ForeignKey(
                        name: "FK_ia_aprendizado_ia_fonte_fonte_id",
                        column: x => x.fonte_id,
                        principalSchema: "smsmarica",
                        principalTable: "ia_fonte",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ia_consulta",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fonte_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pergunta = table.Column<string>(type: "text", nullable: false),
                    sql_gerado = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    visualizacao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    erro = table.Column<string>(type: "text", nullable: true),
                    tokens_entrada = table.Column<int>(type: "integer", nullable: true),
                    tokens_saida = table.Column<int>(type: "integer", nullable: true),
                    duracao_ms = table.Column<int>(type: "integer", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ia_consulta", x => x.id);
                    table.ForeignKey(
                        name: "FK_ia_consulta_ia_fonte_fonte_id",
                        column: x => x.fonte_id,
                        principalSchema: "smsmarica",
                        principalTable: "ia_fonte",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ia_documento_conhecimento",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fonte_id = table.Column<Guid>(type: "uuid", nullable: false),
                    caminho = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    conteudo = table.Column<string>(type: "text", nullable: false),
                    hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ia_documento_conhecimento", x => x.id);
                    table.ForeignKey(
                        name: "FK_ia_documento_conhecimento_ia_fonte_fonte_id",
                        column: x => x.fonte_id,
                        principalSchema: "smsmarica",
                        principalTable: "ia_fonte",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ia_correcao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    consulta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aprendizado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    erro_original = table.Column<string>(type: "text", nullable: false),
                    sql_antes = table.Column<string>(type: "text", nullable: true),
                    sql_depois = table.Column<string>(type: "text", nullable: true),
                    instrucao_gerada = table.Column<string>(type: "text", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    revisado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revisado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    removido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    removido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ia_correcao", x => x.id);
                    table.ForeignKey(
                        name: "FK_ia_correcao_ia_aprendizado_aprendizado_id",
                        column: x => x.aprendizado_id,
                        principalSchema: "smsmarica",
                        principalTable: "ia_aprendizado",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ia_correcao_ia_consulta_consulta_id",
                        column: x => x.consulta_id,
                        principalSchema: "smsmarica",
                        principalTable: "ia_consulta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ia_chunk_conhecimento",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fonte_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    conteudo = table.Column<string>(type: "text", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(1024)", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ia_chunk_conhecimento", x => x.id);
                    table.ForeignKey(
                        name: "FK_ia_chunk_conhecimento_ia_documento_conhecimento_documento_id",
                        column: x => x.documento_id,
                        principalSchema: "smsmarica",
                        principalTable: "ia_documento_conhecimento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ia_aprendizado_fonte_id_ativo",
                schema: "smsmarica",
                table: "ia_aprendizado",
                columns: new[] { "fonte_id", "ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_ia_chunk_conhecimento_documento_id",
                schema: "smsmarica",
                table: "ia_chunk_conhecimento",
                column: "documento_id");

            migrationBuilder.CreateIndex(
                name: "IX_ia_chunk_conhecimento_fonte_id",
                schema: "smsmarica",
                table: "ia_chunk_conhecimento",
                column: "fonte_id");

            migrationBuilder.CreateIndex(
                name: "IX_ia_consulta_criado_em",
                schema: "smsmarica",
                table: "ia_consulta",
                column: "criado_em");

            migrationBuilder.CreateIndex(
                name: "IX_ia_consulta_fonte_id",
                schema: "smsmarica",
                table: "ia_consulta",
                column: "fonte_id");

            migrationBuilder.CreateIndex(
                name: "IX_ia_correcao_aprendizado_id",
                schema: "smsmarica",
                table: "ia_correcao",
                column: "aprendizado_id");

            migrationBuilder.CreateIndex(
                name: "IX_ia_correcao_consulta_id",
                schema: "smsmarica",
                table: "ia_correcao",
                column: "consulta_id");

            migrationBuilder.CreateIndex(
                name: "IX_ia_documento_conhecimento_fonte_id_caminho",
                schema: "smsmarica",
                table: "ia_documento_conhecimento",
                columns: new[] { "fonte_id", "caminho" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ia_fonte_nome",
                schema: "smsmarica",
                table: "ia_fonte",
                column: "nome");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ia_chunk_conhecimento",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ia_configuracao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ia_correcao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ia_documento_conhecimento",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ia_aprendizado",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ia_consulta",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ia_fonte",
                schema: "smsmarica");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
