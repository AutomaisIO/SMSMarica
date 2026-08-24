using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIaConsultaFeedbackEFamilia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "familia",
                schema: "smsmarica",
                table: "ia_fonte",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ia_consulta_feedback",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fonte_id = table.Column<Guid>(type: "uuid", nullable: false),
                    familia = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    pergunta = table.Column<string>(type: "text", nullable: false),
                    resposta = table.Column<string>(type: "text", nullable: true),
                    util = table.Column<bool>(type: "boolean", nullable: false),
                    comentario = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    resolucao = table.Column<string>(type: "text", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    tratado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tratado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ia_consulta_feedback", x => x.id);
                    table.ForeignKey(
                        name: "FK_ia_consulta_feedback_ia_fonte_fonte_id",
                        column: x => x.fonte_id,
                        principalSchema: "smsmarica",
                        principalTable: "ia_fonte",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ia_consulta_feedback_familia",
                schema: "smsmarica",
                table: "ia_consulta_feedback",
                column: "familia");

            migrationBuilder.CreateIndex(
                name: "IX_ia_consulta_feedback_fonte_id",
                schema: "smsmarica",
                table: "ia_consulta_feedback",
                column: "fonte_id");

            migrationBuilder.CreateIndex(
                name: "IX_ia_consulta_feedback_status_criado_em",
                schema: "smsmarica",
                table: "ia_consulta_feedback",
                columns: new[] { "status", "criado_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ia_consulta_feedback",
                schema: "smsmarica");

            migrationBuilder.DropColumn(
                name: "familia",
                schema: "smsmarica",
                table: "ia_fonte");
        }
    }
}
