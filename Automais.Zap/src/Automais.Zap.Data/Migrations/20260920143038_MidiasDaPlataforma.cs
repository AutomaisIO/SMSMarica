using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Zap.Data.Migrations
{
    /// <inheritdoc />
    public partial class MidiasDaPlataforma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "midia",
                schema: "zap",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_arquivo = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    conteudo = table.Column<byte[]>(type: "bytea", nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: false),
                    largura = table.Column<int>(type: "integer", nullable: true),
                    altura = table.Column<int>(type: "integer", nullable: true),
                    hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    categoria = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    criado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_midia", x => x.id);
                    table.ForeignKey(
                        name: "FK_midia_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "zap",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_midia_tenant_hash",
                schema: "zap",
                table: "midia",
                columns: new[] { "tenant_id", "hash_sha256" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "midia",
                schema: "zap");
        }
    }
}
