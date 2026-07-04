using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConfirmacaoAgendamentoWhatsApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "erro_meta",
                schema: "smsmarica",
                table: "tfd_mensagem_whatsapp",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "confirmacao_cancelada_em",
                schema: "smsmarica",
                table: "solicitacao_exame",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "confirmado_canal",
                schema: "smsmarica",
                table: "solicitacao_exame",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "confirmado_em",
                schema: "smsmarica",
                table: "solicitacao_exame",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_cancelamento_paciente",
                schema: "smsmarica",
                table: "solicitacao_exame",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "status_confirmacao",
                schema: "smsmarica",
                table: "solicitacao_exame",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "solicitacao_exame_id",
                schema: "smsmarica",
                table: "cidadao_login_link",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "agendamento_notificacao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    solicitacao_exame_id = table.Column<Guid>(type: "uuid", nullable: true),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    motivo_falha = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    login_link_id = table.Column<Guid>(type: "uuid", nullable: true),
                    mensagem_whatsapp_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tentativas = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ultima_tentativa_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    proxima_tentativa_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    enviado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    entregue_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agendamento_notificacao", x => x.id);
                    table.ForeignKey(
                        name: "FK_agendamento_notificacao_cidadao_login_link_login_link_id",
                        column: x => x.login_link_id,
                        principalSchema: "smsmarica",
                        principalTable: "cidadao_login_link",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_agendamento_notificacao_solicitacao_exame_solicitacao_exame~",
                        column: x => x.solicitacao_exame_id,
                        principalSchema: "smsmarica",
                        principalTable: "solicitacao_exame",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_agendamento_notificacao_tfd_mensagem_whatsapp_mensagem_what~",
                        column: x => x.mensagem_whatsapp_id,
                        principalSchema: "smsmarica",
                        principalTable: "tfd_mensagem_whatsapp",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "agendamento_confirmacao_estado",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    telefone_canonical = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    agendamento_notificacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa = table.Column<int>(type: "integer", nullable: false),
                    expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agendamento_confirmacao_estado", x => x.id);
                    table.ForeignKey(
                        name: "FK_agendamento_confirmacao_estado_agendamento_notificacao_agen~",
                        column: x => x.agendamento_notificacao_id,
                        principalSchema: "smsmarica",
                        principalTable: "agendamento_notificacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agendamento_confirmacao_estado_agendamento_notificacao_id",
                schema: "smsmarica",
                table: "agendamento_confirmacao_estado",
                column: "agendamento_notificacao_id");

            migrationBuilder.CreateIndex(
                name: "IX_agendamento_confirmacao_estado_telefone_canonical",
                schema: "smsmarica",
                table: "agendamento_confirmacao_estado",
                column: "telefone_canonical",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agendamento_notificacao_login_link_id",
                schema: "smsmarica",
                table: "agendamento_notificacao",
                column: "login_link_id");

            migrationBuilder.CreateIndex(
                name: "IX_agendamento_notificacao_mensagem_whatsapp_id",
                schema: "smsmarica",
                table: "agendamento_notificacao",
                column: "mensagem_whatsapp_id");

            migrationBuilder.CreateIndex(
                name: "IX_agendamento_notificacao_paciente_id",
                schema: "smsmarica",
                table: "agendamento_notificacao",
                column: "paciente_id");

            migrationBuilder.CreateIndex(
                name: "IX_agendamento_notificacao_solicitacao_exame_id",
                schema: "smsmarica",
                table: "agendamento_notificacao",
                column: "solicitacao_exame_id",
                unique: true,
                filter: "solicitacao_exame_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_agendamento_notificacao_status_proxima_tentativa_em",
                schema: "smsmarica",
                table: "agendamento_notificacao",
                columns: new[] { "status", "proxima_tentativa_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agendamento_confirmacao_estado",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "agendamento_notificacao",
                schema: "smsmarica");

            migrationBuilder.DropColumn(
                name: "erro_meta",
                schema: "smsmarica",
                table: "tfd_mensagem_whatsapp");

            migrationBuilder.DropColumn(
                name: "confirmacao_cancelada_em",
                schema: "smsmarica",
                table: "solicitacao_exame");

            migrationBuilder.DropColumn(
                name: "confirmado_canal",
                schema: "smsmarica",
                table: "solicitacao_exame");

            migrationBuilder.DropColumn(
                name: "confirmado_em",
                schema: "smsmarica",
                table: "solicitacao_exame");

            migrationBuilder.DropColumn(
                name: "motivo_cancelamento_paciente",
                schema: "smsmarica",
                table: "solicitacao_exame");

            migrationBuilder.DropColumn(
                name: "status_confirmacao",
                schema: "smsmarica",
                table: "solicitacao_exame");

            migrationBuilder.DropColumn(
                name: "solicitacao_exame_id",
                schema: "smsmarica",
                table: "cidadao_login_link");
        }
    }
}
