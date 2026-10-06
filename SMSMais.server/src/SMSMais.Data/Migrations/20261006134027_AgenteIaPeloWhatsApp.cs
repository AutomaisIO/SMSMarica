using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgenteIaPeloWhatsApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "agente_ia",
                schema: "smsmarica",
                table: "alerta_destinatario",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "agente_usuario_id",
                schema: "smsmarica",
                table: "alerta_destinatario",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "agente_whatsapp_pedido",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mensagem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    texto = table.Column<string>(type: "text", nullable: false),
                    citado = table.Column<string>(type: "text", nullable: true),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    sessao_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    turno_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    cursor = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    andamento_pendente = table.Column<string>(type: "text", nullable: true),
                    ultimo_andamento_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    iniciado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    concluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    erro = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agente_whatsapp_pedido", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alerta_destinatario_agente_usuario_id",
                schema: "smsmarica",
                table: "alerta_destinatario",
                column: "agente_usuario_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_alerta_destinatario_agente_usuario",
                schema: "smsmarica",
                table: "alerta_destinatario",
                sql: "NOT agente_ia OR agente_usuario_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_agente_whatsapp_pedido_situacao",
                schema: "smsmarica",
                table: "agente_whatsapp_pedido",
                columns: new[] { "situacao", "criado_em" });

            migrationBuilder.CreateIndex(
                name: "ux_agente_whatsapp_pedido_mensagem",
                schema: "smsmarica",
                table: "agente_whatsapp_pedido",
                column: "mensagem_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_alerta_destinatario_agente_usuario",
                schema: "smsmarica",
                table: "alerta_destinatario",
                column: "agente_usuario_id",
                principalSchema: "smsmarica",
                principalTable: "usuario",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_alerta_destinatario_agente_usuario",
                schema: "smsmarica",
                table: "alerta_destinatario");

            migrationBuilder.DropTable(
                name: "agente_whatsapp_pedido",
                schema: "smsmarica");

            migrationBuilder.DropIndex(
                name: "IX_alerta_destinatario_agente_usuario_id",
                schema: "smsmarica",
                table: "alerta_destinatario");

            migrationBuilder.DropCheckConstraint(
                name: "ck_alerta_destinatario_agente_usuario",
                schema: "smsmarica",
                table: "alerta_destinatario");

            migrationBuilder.DropColumn(
                name: "agente_ia",
                schema: "smsmarica",
                table: "alerta_destinatario");

            migrationBuilder.DropColumn(
                name: "agente_usuario_id",
                schema: "smsmarica",
                table: "alerta_destinatario");
        }
    }
}
