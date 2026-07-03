using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarModuloTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ticket",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    prioridade = table.Column<int>(type: "integer", nullable: false),
                    resposta_final = table.Column<string>(type: "text", nullable: true),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    arquivado_pelo_autor_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    arquivado_pelo_admin_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_ticket", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ticket_configuracao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    visibilidade = table.Column<int>(type: "integer", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_configuracao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ticket_comentario",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    autor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    texto = table.Column<string>(type: "text", nullable: false),
                    interno = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_comentario", x => x.id);
                    table.ForeignKey(
                        name: "FK_ticket_comentario_ticket_ticket_id",
                        column: x => x.ticket_id,
                        principalSchema: "smsmarica",
                        principalTable: "ticket",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ticket_anexo",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comentario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    midia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_arquivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_anexo", x => x.id);
                    table.ForeignKey(
                        name: "FK_ticket_anexo_ticket_comentario_comentario_id",
                        column: x => x.comentario_id,
                        principalSchema: "smsmarica",
                        principalTable: "ticket_comentario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ticket_anexo_ticket_ticket_id",
                        column: x => x.ticket_id,
                        principalSchema: "smsmarica",
                        principalTable: "ticket",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ticket_criado_por",
                schema: "smsmarica",
                table: "ticket",
                column: "criado_por");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_status",
                schema: "smsmarica",
                table: "ticket",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_unidade_id",
                schema: "smsmarica",
                table: "ticket",
                column: "unidade_id");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_anexo_comentario_id",
                schema: "smsmarica",
                table: "ticket_anexo",
                column: "comentario_id");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_anexo_ticket_id",
                schema: "smsmarica",
                table: "ticket_anexo",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_comentario_ticket_id",
                schema: "smsmarica",
                table: "ticket_comentario",
                column: "ticket_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_anexo",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ticket_configuracao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ticket_comentario",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ticket",
                schema: "smsmarica");
        }
    }
}
