using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class CriarMedicos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "medico",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    crm = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    uf_crm = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    especialidade = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    rqe = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    validade_crm = table.Column<DateOnly>(type: "date", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medico", x => x.id);
                    table.ForeignKey(
                        name: "FK_medico_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "smsmarica",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_medico_crm_uf_crm",
                schema: "smsmarica",
                table: "medico",
                columns: new[] { "crm", "uf_crm" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_medico_usuario_id",
                schema: "smsmarica",
                table: "medico",
                column: "usuario_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "medico",
                schema: "smsmarica");
        }
    }
}
