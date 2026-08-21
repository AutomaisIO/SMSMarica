using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Zap.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConfiguracaoMetaEWaba : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "configuracao_meta",
                schema: "zap",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    app_id = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    app_secret_cifrado = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    verify_token_cifrado = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    token_sistema_cifrado = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    base_url = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracao_meta", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "waba",
                schema: "zap",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    waba_id = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    destino_id = table.Column<Guid>(type: "uuid", nullable: true),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sincronizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_waba", x => x.id);
                    table.ForeignKey(
                        name: "FK_waba_destino_destino_id",
                        column: x => x.destino_id,
                        principalSchema: "zap",
                        principalTable: "destino",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_waba_destino_id",
                schema: "zap",
                table: "waba",
                column: "destino_id");

            migrationBuilder.CreateIndex(
                name: "ux_waba_waba_id",
                schema: "zap",
                table: "waba",
                column: "waba_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuracao_meta",
                schema: "zap");

            migrationBuilder.DropTable(
                name: "waba",
                schema: "zap");
        }
    }
}
