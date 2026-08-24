using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarModuloConversas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "autor_nome_exibicao",
                schema: "smsmarica",
                table: "tfd_mensagem_whatsapp",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "autor_usuario_id",
                schema: "smsmarica",
                table: "tfd_mensagem_whatsapp",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "conversa_id",
                schema: "smsmarica",
                table: "tfd_mensagem_whatsapp",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tipo_mensagem",
                schema: "smsmarica",
                table: "tfd_mensagem_whatsapp",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "conversa",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    canal = table.Column<int>(type: "integer", nullable: false),
                    telefone_canonical = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nome_contato = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    assunto = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    operador_responsavel_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    janela_expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ultima_mensagem_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ultima_mensagem_direcao = table.Column<int>(type: "integer", nullable: true),
                    ultima_mensagem_preview = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    nao_lidas = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    primeiro_contato_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversa", x => x.id);
                    table.ForeignKey(
                        name: "FK_conversa_unidade_unidade_id",
                        column: x => x.unidade_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_conversa_usuario_operador_responsavel_id",
                        column: x => x.operador_responsavel_id,
                        principalSchema: "smsmarica",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "usuario_unidade",
                schema: "smsmarica",
                columns: table => new
                {
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario_unidade", x => new { x.usuario_id, x.unidade_id });
                    table.ForeignKey(
                        name: "FK_usuario_unidade_unidade_unidade_id",
                        column: x => x.unidade_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_usuario_unidade_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "smsmarica",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "conversa_evento",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    ator_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    de_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    para_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    de_unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    para_unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ocorrido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversa_evento", x => x.id);
                    table.ForeignKey(
                        name: "FK_conversa_evento_conversa_conversa_id",
                        column: x => x.conversa_id,
                        principalSchema: "smsmarica",
                        principalTable: "conversa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tfd_mensagem_whatsapp_conversa_id_ocorrido_em",
                schema: "smsmarica",
                table: "tfd_mensagem_whatsapp",
                columns: new[] { "conversa_id", "ocorrido_em" });

            migrationBuilder.CreateIndex(
                name: "IX_conversa_operador_responsavel_id_status",
                schema: "smsmarica",
                table: "conversa",
                columns: new[] { "operador_responsavel_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_conversa_telefone_canonical",
                schema: "smsmarica",
                table: "conversa",
                column: "telefone_canonical");

            migrationBuilder.CreateIndex(
                name: "IX_conversa_telefone_canonical_canal",
                schema: "smsmarica",
                table: "conversa",
                columns: new[] { "telefone_canonical", "canal" },
                unique: true,
                filter: "status IN (1, 2) AND excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_conversa_unidade_id_ultima_mensagem_em",
                schema: "smsmarica",
                table: "conversa",
                columns: new[] { "unidade_id", "ultima_mensagem_em" });

            migrationBuilder.CreateIndex(
                name: "IX_conversa_evento_conversa_id_ocorrido_em",
                schema: "smsmarica",
                table: "conversa_evento",
                columns: new[] { "conversa_id", "ocorrido_em" });

            migrationBuilder.CreateIndex(
                name: "IX_usuario_unidade_unidade_id",
                schema: "smsmarica",
                table: "usuario_unidade",
                column: "unidade_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_unidade_usuario_id",
                schema: "smsmarica",
                table: "usuario_unidade",
                column: "usuario_id",
                unique: true,
                filter: "principal = true");

            migrationBuilder.AddForeignKey(
                name: "FK_tfd_mensagem_whatsapp_conversa_conversa_id",
                schema: "smsmarica",
                table: "tfd_mensagem_whatsapp",
                column: "conversa_id",
                principalSchema: "smsmarica",
                principalTable: "conversa",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tfd_mensagem_whatsapp_conversa_conversa_id",
                schema: "smsmarica",
                table: "tfd_mensagem_whatsapp");

            migrationBuilder.DropTable(
                name: "conversa_evento",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "usuario_unidade",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "conversa",
                schema: "smsmarica");

            migrationBuilder.DropIndex(
                name: "IX_tfd_mensagem_whatsapp_conversa_id_ocorrido_em",
                schema: "smsmarica",
                table: "tfd_mensagem_whatsapp");

            migrationBuilder.DropColumn(
                name: "autor_nome_exibicao",
                schema: "smsmarica",
                table: "tfd_mensagem_whatsapp");

            migrationBuilder.DropColumn(
                name: "autor_usuario_id",
                schema: "smsmarica",
                table: "tfd_mensagem_whatsapp");

            migrationBuilder.DropColumn(
                name: "conversa_id",
                schema: "smsmarica",
                table: "tfd_mensagem_whatsapp");

            migrationBuilder.DropColumn(
                name: "tipo_mensagem",
                schema: "smsmarica",
                table: "tfd_mensagem_whatsapp");
        }
    }
}
