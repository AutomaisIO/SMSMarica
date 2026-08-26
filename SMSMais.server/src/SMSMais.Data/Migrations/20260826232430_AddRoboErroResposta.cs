using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoboErroResposta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "robo_erro_resposta",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mensagem_whatsapp_id = table.Column<Guid>(type: "uuid", nullable: true),
                    robo_assunto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    trecho = table.Column<string>(type: "text", nullable: true),
                    nota = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    revisado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revisado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    revisao_nota = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_robo_erro_resposta", x => x.id);
                    table.ForeignKey(
                        name: "FK_robo_erro_resposta_conversa_conversa_id",
                        column: x => x.conversa_id,
                        principalSchema: "smsmarica",
                        principalTable: "conversa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_robo_erro_resposta_robo_assunto_robo_assunto_id",
                        column: x => x.robo_assunto_id,
                        principalSchema: "smsmarica",
                        principalTable: "robo_assunto",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_robo_erro_resposta_whatsapp_mensagem_mensagem_whatsapp_id",
                        column: x => x.mensagem_whatsapp_id,
                        principalSchema: "smsmarica",
                        principalTable: "whatsapp_mensagem",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_robo_erro_resposta_conversa_id",
                schema: "smsmarica",
                table: "robo_erro_resposta",
                column: "conversa_id");

            migrationBuilder.CreateIndex(
                name: "IX_robo_erro_resposta_robo_assunto_id",
                schema: "smsmarica",
                table: "robo_erro_resposta",
                column: "robo_assunto_id");

            migrationBuilder.CreateIndex(
                name: "ix_robo_erro_status_criado",
                schema: "smsmarica",
                table: "robo_erro_resposta",
                columns: new[] { "status", "criado_em" });

            migrationBuilder.CreateIndex(
                name: "ux_robo_erro_mensagem_aberto",
                schema: "smsmarica",
                table: "robo_erro_resposta",
                column: "mensagem_whatsapp_id",
                unique: true,
                filter: "mensagem_whatsapp_id IS NOT NULL AND status = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "robo_erro_resposta",
                schema: "smsmarica");
        }
    }
}
