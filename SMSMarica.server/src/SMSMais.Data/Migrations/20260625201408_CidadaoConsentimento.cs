using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class CidadaoConsentimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cidadao_consentimento",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cidadao_acesso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    texto_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    aceito_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    dispositivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    revogado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cidadao_consentimento", x => x.id);
                    table.ForeignKey(
                        name: "FK_cidadao_consentimento_cidadao_acesso_cidadao_acesso_id",
                        column: x => x.cidadao_acesso_id,
                        principalSchema: "smsmarica",
                        principalTable: "cidadao_acesso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cidadao_consentimento_vigente",
                schema: "smsmarica",
                table: "cidadao_consentimento",
                columns: new[] { "cidadao_acesso_id", "versao", "revogado_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cidadao_consentimento",
                schema: "smsmarica");
        }
    }
}
