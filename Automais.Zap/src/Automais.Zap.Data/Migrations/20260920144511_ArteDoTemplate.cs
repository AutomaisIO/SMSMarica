using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Zap.Data.Migrations
{
    /// <inheritdoc />
    public partial class ArteDoTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "template_arte",
                schema: "zap",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    waba_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    midia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_template_arte", x => x.id);
                    table.ForeignKey(
                        name: "FK_template_arte_midia_midia_id",
                        column: x => x.midia_id,
                        principalSchema: "zap",
                        principalTable: "midia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_template_arte_waba_waba_id",
                        column: x => x.waba_id,
                        principalSchema: "zap",
                        principalTable: "waba",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_template_arte_midia_id",
                schema: "zap",
                table: "template_arte",
                column: "midia_id");

            migrationBuilder.CreateIndex(
                name: "ux_template_arte_waba_template",
                schema: "zap",
                table: "template_arte",
                columns: new[] { "waba_id", "template" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "template_arte",
                schema: "zap");
        }
    }
}
