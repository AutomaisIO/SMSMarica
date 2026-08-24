using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AnexosExame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "anexo_upload_token",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    solicitacao_exame_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paciente_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revogado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ultimo_uso_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anexo_upload_token", x => x.id);
                    table.ForeignKey(
                        name: "FK_anexo_upload_token_solicitacao_exame_solicitacao_exame_id",
                        column: x => x.solicitacao_exame_id,
                        principalSchema: "smsmarica",
                        principalTable: "solicitacao_exame",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documento_exame",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitacao_exame_id = table.Column<Guid>(type: "uuid", nullable: false),
                    anexo_upload_token_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    mime_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: false),
                    hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    chave_armazenamento = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    origem = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    paginas = table.Column<int>(type: "integer", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documento_exame", x => x.id);
                    table.ForeignKey(
                        name: "FK_documento_exame_anexo_upload_token_anexo_upload_token_id",
                        column: x => x.anexo_upload_token_id,
                        principalSchema: "smsmarica",
                        principalTable: "anexo_upload_token",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_documento_exame_solicitacao_exame_solicitacao_exame_id",
                        column: x => x.solicitacao_exame_id,
                        principalSchema: "smsmarica",
                        principalTable: "solicitacao_exame",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_anexo_upload_token_solicitacao_exame_id",
                schema: "smsmarica",
                table: "anexo_upload_token",
                column: "solicitacao_exame_id");

            migrationBuilder.CreateIndex(
                name: "ix_anexo_upload_token_token",
                schema: "smsmarica",
                table: "anexo_upload_token",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_documento_exame_anexo_upload_token_id",
                schema: "smsmarica",
                table: "documento_exame",
                column: "anexo_upload_token_id");

            migrationBuilder.CreateIndex(
                name: "IX_documento_exame_hash_sha256",
                schema: "smsmarica",
                table: "documento_exame",
                column: "hash_sha256");

            migrationBuilder.CreateIndex(
                name: "ix_documento_exame_solicitacao_exame_id",
                schema: "smsmarica",
                table: "documento_exame",
                column: "solicitacao_exame_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "documento_exame",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "anexo_upload_token",
                schema: "smsmarica");
        }
    }
}
