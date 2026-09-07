using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotificacoesRegulacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "regulacao_evento_visto",
                schema: "smsmarica",
                columns: table => new
                {
                    evento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    visto_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulacao_evento_visto", x => new { x.evento_id, x.usuario_id });
                    table.ForeignKey(
                        name: "FK_regulacao_evento_visto_regulacao_evento_evento_id",
                        column: x => x.evento_id,
                        principalSchema: "smsmarica",
                        principalTable: "regulacao_evento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_regulacao_evento_visto_usuario",
                schema: "smsmarica",
                table: "regulacao_evento_visto",
                column: "usuario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "regulacao_evento_visto",
                schema: "smsmarica");
        }
    }
}
