using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class CidadaoAcessoESessao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cidadao_acesso",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    senha_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    google_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    microsoft_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    facebook_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cidadao_acesso", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cidadao_sessao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cidadao_acesso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    canal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    dispositivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    criada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revogada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cidadao_sessao", x => x.id);
                    table.ForeignKey(
                        name: "FK_cidadao_sessao_cidadao_acesso_cidadao_acesso_id",
                        column: x => x.cidadao_acesso_id,
                        principalSchema: "smsmarica",
                        principalTable: "cidadao_acesso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_cidadao_acesso_cpf",
                schema: "smsmarica",
                table: "cidadao_acesso",
                column: "cpf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_cidadao_acesso_facebook",
                schema: "smsmarica",
                table: "cidadao_acesso",
                column: "facebook_sub",
                unique: true,
                filter: "facebook_sub IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_cidadao_acesso_google",
                schema: "smsmarica",
                table: "cidadao_acesso",
                column: "google_sub",
                unique: true,
                filter: "google_sub IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_cidadao_acesso_microsoft",
                schema: "smsmarica",
                table: "cidadao_acesso",
                column: "microsoft_sub",
                unique: true,
                filter: "microsoft_sub IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_cidadao_acesso_patient",
                schema: "smsmarica",
                table: "cidadao_acesso",
                column: "patient_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cidadao_sessao_acesso_ativa",
                schema: "smsmarica",
                table: "cidadao_sessao",
                columns: new[] { "cidadao_acesso_id", "revogada_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cidadao_sessao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "cidadao_acesso",
                schema: "smsmarica");
        }
    }
}
