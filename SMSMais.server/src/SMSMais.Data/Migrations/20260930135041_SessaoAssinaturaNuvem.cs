using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class SessaoAssinaturaNuvem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "nuvem_sessao_expira_em",
                schema: "smsmarica",
                table: "laudo_assinatura",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "nuvem_sessao_id",
                schema: "smsmarica",
                table: "laudo_assinatura",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sessao_login_id",
                schema: "smsmarica",
                table: "laudo_assinatura",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "assinatura_nuvem_sessao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    medico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sessao_login_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    credencial_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    code_verifier = table.Column<string>(type: "text", nullable: true),
                    cert_thumbprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    autorizada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    autorizada_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    encerrada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    motivo_encerramento = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    encerrada_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assinatura_nuvem_sessao", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_laudo_assinatura_nuvem_sessao_id",
                schema: "smsmarica",
                table: "laudo_assinatura",
                column: "nuvem_sessao_id");

            migrationBuilder.CreateIndex(
                name: "ix_assinatura_nuvem_sessao_login_aberta",
                schema: "smsmarica",
                table: "assinatura_nuvem_sessao",
                columns: new[] { "sessao_login_id", "medico_id" },
                unique: true,
                filter: "encerrada_em IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_laudo_assinatura_assinatura_nuvem_sessao_nuvem_sessao_id",
                schema: "smsmarica",
                table: "laudo_assinatura",
                column: "nuvem_sessao_id",
                principalSchema: "smsmarica",
                principalTable: "assinatura_nuvem_sessao",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_laudo_assinatura_assinatura_nuvem_sessao_nuvem_sessao_id",
                schema: "smsmarica",
                table: "laudo_assinatura");

            migrationBuilder.DropTable(
                name: "assinatura_nuvem_sessao",
                schema: "smsmarica");

            migrationBuilder.DropIndex(
                name: "IX_laudo_assinatura_nuvem_sessao_id",
                schema: "smsmarica",
                table: "laudo_assinatura");

            migrationBuilder.DropColumn(
                name: "nuvem_sessao_expira_em",
                schema: "smsmarica",
                table: "laudo_assinatura");

            migrationBuilder.DropColumn(
                name: "nuvem_sessao_id",
                schema: "smsmarica",
                table: "laudo_assinatura");

            migrationBuilder.DropColumn(
                name: "sessao_login_id",
                schema: "smsmarica",
                table: "laudo_assinatura");
        }
    }
}
