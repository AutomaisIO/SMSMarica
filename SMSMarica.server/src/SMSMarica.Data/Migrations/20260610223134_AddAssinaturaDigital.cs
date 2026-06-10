using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssinaturaDigital : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "laudo_assinatura",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    laudo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    medico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chave_agente = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    chave_expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    pdf_assinado = table.Column<byte[]>(type: "bytea", nullable: true),
                    pdf_hash_sha256 = table.Column<byte[]>(type: "bytea", nullable: true),
                    transfer_state = table.Column<byte[]>(type: "bytea", nullable: true),
                    hash_para_assinar = table.Column<byte[]>(type: "bytea", nullable: true),
                    cert_thumbprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    entregue_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    assinado_por_cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    assinado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    certificado_titular = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    certificado_emissor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    formato = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    com_carimbo_tempo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    assinado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_laudo_assinatura", x => x.id);
                    table.ForeignKey(
                        name: "FK_laudo_assinatura_laudo_laudo_id",
                        column: x => x.laudo_id,
                        principalSchema: "smsmarica",
                        principalTable: "laudo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_laudo_assinatura_chave_agente",
                schema: "smsmarica",
                table: "laudo_assinatura",
                column: "chave_agente");

            migrationBuilder.CreateIndex(
                name: "ix_laudo_assinatura_laudo_id_concluida",
                schema: "smsmarica",
                table: "laudo_assinatura",
                column: "laudo_id",
                unique: true,
                filter: "status = 2");

            migrationBuilder.CreateIndex(
                name: "IX_laudo_assinatura_laudo_id_status",
                schema: "smsmarica",
                table: "laudo_assinatura",
                columns: new[] { "laudo_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "laudo_assinatura",
                schema: "smsmarica");
        }
    }
}
