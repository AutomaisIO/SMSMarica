using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <summary>
    /// Renomeia agendamento_notificacao → comunicacao_paciente PRESERVANDO os dados (o scaffold
    /// geraria Drop/Create). Adiciona finalidade (default 1 = ConfirmacaoAgendamento, backfill
    /// automático) + visualizado_em, troca o unique para (solicitacao, finalidade) e cria a
    /// contato_registro. Constraints/índices renomeados para os nomes que o snapshot espera.
    /// </summary>
    public partial class RenomearComunicacaoPaciente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- Renames estruturais (dados preservados) ----
            migrationBuilder.RenameTable(
                name: "agendamento_notificacao",
                schema: "smsmarica",
                newName: "comunicacao_paciente",
                newSchema: "smsmarica");

            migrationBuilder.RenameColumn(
                name: "agendamento_notificacao_id",
                schema: "smsmarica",
                table: "agendamento_confirmacao_estado",
                newName: "comunicacao_paciente_id");

            // ---- Constraints: nomes antigos (alguns truncados pelo PG com '~') → nomes do snapshot ----
            migrationBuilder.Sql("""
                ALTER TABLE smsmarica.comunicacao_paciente RENAME CONSTRAINT "PK_agendamento_notificacao" TO "PK_comunicacao_paciente";
                ALTER TABLE smsmarica.comunicacao_paciente RENAME CONSTRAINT "FK_agendamento_notificacao_cidadao_login_link_login_link_id" TO "FK_comunicacao_paciente_cidadao_login_link_login_link_id";
                ALTER TABLE smsmarica.comunicacao_paciente RENAME CONSTRAINT "FK_agendamento_notificacao_solicitacao_exame_solicitacao_exame~" TO "FK_comunicacao_paciente_solicitacao_exame_solicitacao_exame_id";
                ALTER TABLE smsmarica.comunicacao_paciente RENAME CONSTRAINT "FK_agendamento_notificacao_tfd_mensagem_whatsapp_mensagem_what~" TO "FK_comunicacao_paciente_tfd_mensagem_whatsapp_mensagem_whatsap~";
                ALTER TABLE smsmarica.agendamento_confirmacao_estado RENAME CONSTRAINT "FK_agendamento_confirmacao_estado_agendamento_notificacao_agen~" TO "FK_agendamento_confirmacao_estado_comunicacao_paciente_comunic~";
                """);

            // ---- Índices ----
            migrationBuilder.RenameIndex(
                name: "IX_agendamento_confirmacao_estado_agendamento_notificacao_id",
                schema: "smsmarica",
                table: "agendamento_confirmacao_estado",
                newName: "IX_agendamento_confirmacao_estado_comunicacao_paciente_id");
            migrationBuilder.RenameIndex(
                name: "IX_agendamento_notificacao_login_link_id",
                schema: "smsmarica",
                table: "comunicacao_paciente",
                newName: "IX_comunicacao_paciente_login_link_id");
            migrationBuilder.RenameIndex(
                name: "IX_agendamento_notificacao_mensagem_whatsapp_id",
                schema: "smsmarica",
                table: "comunicacao_paciente",
                newName: "IX_comunicacao_paciente_mensagem_whatsapp_id");
            migrationBuilder.RenameIndex(
                name: "IX_agendamento_notificacao_paciente_id",
                schema: "smsmarica",
                table: "comunicacao_paciente",
                newName: "IX_comunicacao_paciente_paciente_id");
            migrationBuilder.RenameIndex(
                name: "IX_agendamento_notificacao_status_proxima_tentativa_em",
                schema: "smsmarica",
                table: "comunicacao_paciente",
                newName: "IX_comunicacao_paciente_status_proxima_tentativa_em");

            // ---- Colunas novas ----
            migrationBuilder.AddColumn<int>(
                name: "finalidade",
                schema: "smsmarica",
                table: "comunicacao_paciente",
                type: "integer",
                nullable: false,
                defaultValue: 1); // backfill: linhas existentes são ConfirmacaoAgendamento

            migrationBuilder.AddColumn<DateTime>(
                name: "visualizado_em",
                schema: "smsmarica",
                table: "comunicacao_paciente",
                type: "timestamp with time zone",
                nullable: true);

            // ---- Unique: por solicitação → por (solicitação, finalidade) ----
            migrationBuilder.DropIndex(
                name: "IX_agendamento_notificacao_solicitacao_exame_id",
                schema: "smsmarica",
                table: "comunicacao_paciente");

            migrationBuilder.CreateIndex(
                name: "IX_comunicacao_paciente_solicitacao_exame_id_finalidade",
                schema: "smsmarica",
                table: "comunicacao_paciente",
                columns: new[] { "solicitacao_exame_id", "finalidade" },
                unique: true,
                filter: "solicitacao_exame_id IS NOT NULL");

            // ---- Registro manual de contatos (append-only) ----
            migrationBuilder.CreateTable(
                name: "contato_registro",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitacao_exame_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    meio = table.Column<int>(type: "integer", nullable: false),
                    resultado = table.Column<int>(type: "integer", nullable: false),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contato_registro", x => x.id);
                    table.ForeignKey(
                        name: "FK_contato_registro_solicitacao_exame_solicitacao_exame_id",
                        column: x => x.solicitacao_exame_id,
                        principalSchema: "smsmarica",
                        principalTable: "solicitacao_exame",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contato_registro_solicitacao_exame_id_criado_em",
                schema: "smsmarica",
                table: "contato_registro",
                columns: new[] { "solicitacao_exame_id", "criado_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contato_registro",
                schema: "smsmarica");

            migrationBuilder.DropIndex(
                name: "IX_comunicacao_paciente_solicitacao_exame_id_finalidade",
                schema: "smsmarica",
                table: "comunicacao_paciente");

            migrationBuilder.CreateIndex(
                name: "IX_agendamento_notificacao_solicitacao_exame_id",
                schema: "smsmarica",
                table: "comunicacao_paciente",
                column: "solicitacao_exame_id",
                unique: true,
                filter: "solicitacao_exame_id IS NOT NULL");

            migrationBuilder.DropColumn(name: "finalidade", schema: "smsmarica", table: "comunicacao_paciente");
            migrationBuilder.DropColumn(name: "visualizado_em", schema: "smsmarica", table: "comunicacao_paciente");

            migrationBuilder.Sql("""
                ALTER TABLE smsmarica.comunicacao_paciente RENAME CONSTRAINT "PK_comunicacao_paciente" TO "PK_agendamento_notificacao";
                ALTER TABLE smsmarica.comunicacao_paciente RENAME CONSTRAINT "FK_comunicacao_paciente_cidadao_login_link_login_link_id" TO "FK_agendamento_notificacao_cidadao_login_link_login_link_id";
                ALTER TABLE smsmarica.comunicacao_paciente RENAME CONSTRAINT "FK_comunicacao_paciente_solicitacao_exame_solicitacao_exame_id" TO "FK_agendamento_notificacao_solicitacao_exame_solicitacao_exame~";
                ALTER TABLE smsmarica.comunicacao_paciente RENAME CONSTRAINT "FK_comunicacao_paciente_tfd_mensagem_whatsapp_mensagem_whatsap~" TO "FK_agendamento_notificacao_tfd_mensagem_whatsapp_mensagem_what~";
                ALTER TABLE smsmarica.agendamento_confirmacao_estado RENAME CONSTRAINT "FK_agendamento_confirmacao_estado_comunicacao_paciente_comunic~" TO "FK_agendamento_confirmacao_estado_agendamento_notificacao_agen~";
                """);

            migrationBuilder.RenameIndex(
                name: "IX_comunicacao_paciente_login_link_id",
                schema: "smsmarica", table: "comunicacao_paciente",
                newName: "IX_agendamento_notificacao_login_link_id");
            migrationBuilder.RenameIndex(
                name: "IX_comunicacao_paciente_mensagem_whatsapp_id",
                schema: "smsmarica", table: "comunicacao_paciente",
                newName: "IX_agendamento_notificacao_mensagem_whatsapp_id");
            migrationBuilder.RenameIndex(
                name: "IX_comunicacao_paciente_paciente_id",
                schema: "smsmarica", table: "comunicacao_paciente",
                newName: "IX_agendamento_notificacao_paciente_id");
            migrationBuilder.RenameIndex(
                name: "IX_comunicacao_paciente_status_proxima_tentativa_em",
                schema: "smsmarica", table: "comunicacao_paciente",
                newName: "IX_agendamento_notificacao_status_proxima_tentativa_em");
            migrationBuilder.RenameIndex(
                name: "IX_agendamento_confirmacao_estado_comunicacao_paciente_id",
                schema: "smsmarica", table: "agendamento_confirmacao_estado",
                newName: "IX_agendamento_confirmacao_estado_agendamento_notificacao_id");

            migrationBuilder.RenameColumn(
                name: "comunicacao_paciente_id",
                schema: "smsmarica",
                table: "agendamento_confirmacao_estado",
                newName: "agendamento_notificacao_id");

            migrationBuilder.RenameTable(
                name: "comunicacao_paciente",
                schema: "smsmarica",
                newName: "agendamento_notificacao",
                newSchema: "smsmarica");
        }
    }
}
