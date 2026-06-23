using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class CabecalhoRodapeLaudoEMidia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "laudo_configuracao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cabecalho_json = table.Column<string>(type: "jsonb", nullable: false),
                    cabecalho_html = table.Column<string>(type: "text", nullable: false),
                    rodape_json = table.Column<string>(type: "jsonb", nullable: false),
                    rodape_html = table.Column<string>(type: "text", nullable: false),
                    atualizado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_laudo_configuracao", x => x.id);
                    table.ForeignKey(
                        name: "FK_laudo_configuracao_usuario_atualizado_por_usuario_id",
                        column: x => x.atualizado_por_usuario_id,
                        principalSchema: "smsmarica",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "midia",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_arquivo = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    conteudo = table.Column<byte[]>(type: "bytea", nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: false),
                    largura = table.Column<int>(type: "integer", nullable: true),
                    altura = table.Column<int>(type: "integer", nullable: true),
                    categoria = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    criado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_midia", x => x.id);
                    table.ForeignKey(
                        name: "FK_midia_usuario_criado_por_usuario_id",
                        column: x => x.criado_por_usuario_id,
                        principalSchema: "smsmarica",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_laudo_configuracao_atualizado_por_usuario_id",
                schema: "smsmarica",
                table: "laudo_configuracao",
                column: "atualizado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_midia_categoria",
                schema: "smsmarica",
                table: "midia",
                column: "categoria");

            migrationBuilder.CreateIndex(
                name: "IX_midia_criado_por_usuario_id",
                schema: "smsmarica",
                table: "midia",
                column: "criado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_midia_hash_sha256",
                schema: "smsmarica",
                table: "midia",
                column: "hash_sha256");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "laudo_configuracao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "midia",
                schema: "smsmarica");
        }
    }
}
