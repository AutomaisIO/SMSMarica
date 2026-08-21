using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Zap.Data.Migrations
{
    /// <inheritdoc />
    public partial class TokensDeTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_token",
                schema: "zap",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    prefixo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    todos_numeros = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ultimo_uso_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revogado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_token", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenant_token_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "zap",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_token_numero",
                schema: "zap",
                columns: table => new
                {
                    token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_token_numero", x => new { x.token_id, x.numero_id });
                    table.ForeignKey(
                        name: "FK_tenant_token_numero_numero_numero_id",
                        column: x => x.numero_id,
                        principalSchema: "zap",
                        principalTable: "numero",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tenant_token_numero_tenant_token_token_id",
                        column: x => x.token_id,
                        principalSchema: "zap",
                        principalTable: "tenant_token",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_token_tenant_id",
                schema: "zap",
                table: "tenant_token",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_tenant_token_prefixo",
                schema: "zap",
                table: "tenant_token",
                column: "prefixo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_token_numero_numero_id",
                schema: "zap",
                table: "tenant_token_numero",
                column: "numero_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_token_numero",
                schema: "zap");

            migrationBuilder.DropTable(
                name: "tenant_token",
                schema: "zap");
        }
    }
}
