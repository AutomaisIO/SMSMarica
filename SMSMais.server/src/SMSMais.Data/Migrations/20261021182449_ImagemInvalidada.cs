using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class ImagemInvalidada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "imagem_invalidada",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    study_instance_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    sop_instance_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_imagem_invalidada", x => x.id);
                    table.ForeignKey(
                        name: "FK_imagem_invalidada_usuario_criado_por",
                        column: x => x.criado_por,
                        principalSchema: "smsmarica",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_imagem_invalidada_criado_por",
                schema: "smsmarica",
                table: "imagem_invalidada",
                column: "criado_por");

            migrationBuilder.CreateIndex(
                name: "IX_imagem_invalidada_study_instance_uid",
                schema: "smsmarica",
                table: "imagem_invalidada",
                column: "study_instance_uid");

            migrationBuilder.CreateIndex(
                name: "IX_imagem_invalidada_study_instance_uid_sop_instance_uid",
                schema: "smsmarica",
                table: "imagem_invalidada",
                columns: new[] { "study_instance_uid", "sop_instance_uid" },
                unique: true,
                filter: "excluido_em IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "imagem_invalidada",
                schema: "smsmarica");
        }
    }
}
