using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class ModosAssinaturaMedicoESeloLaudo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "modo",
                schema: "smsmarica",
                table: "laudo_assinatura",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nuvem_code_verifier",
                schema: "smsmarica",
                table: "laudo_assinatura",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nuvem_state_hash",
                schema: "smsmarica",
                table: "laudo_assinatura",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "laudo_verificacao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    laudo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_laudo_verificacao", x => x.id);
                    table.ForeignKey(
                        name: "FK_laudo_verificacao_laudo_laudo_id",
                        column: x => x.laudo_id,
                        principalSchema: "smsmarica",
                        principalTable: "laudo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "medico_config_assinatura",
                schema: "smsmarica",
                columns: table => new
                {
                    medico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modo = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medico_config_assinatura", x => x.medico_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_laudo_assinatura_nuvem_state_hash",
                schema: "smsmarica",
                table: "laudo_assinatura",
                column: "nuvem_state_hash",
                filter: "nuvem_state_hash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_laudo_verificacao_laudo_id",
                schema: "smsmarica",
                table: "laudo_verificacao",
                column: "laudo_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "laudo_verificacao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "medico_config_assinatura",
                schema: "smsmarica");

            migrationBuilder.DropIndex(
                name: "IX_laudo_assinatura_nuvem_state_hash",
                schema: "smsmarica",
                table: "laudo_assinatura");

            migrationBuilder.DropColumn(
                name: "modo",
                schema: "smsmarica",
                table: "laudo_assinatura");

            migrationBuilder.DropColumn(
                name: "nuvem_code_verifier",
                schema: "smsmarica",
                table: "laudo_assinatura");

            migrationBuilder.DropColumn(
                name: "nuvem_state_hash",
                schema: "smsmarica",
                table: "laudo_assinatura");
        }
    }
}
