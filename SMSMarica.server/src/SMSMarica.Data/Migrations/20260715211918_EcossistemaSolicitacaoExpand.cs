using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class EcossistemaSolicitacaoExpand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "solicitacao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    categoria = table.Column<int>(type: "integer", nullable: false),
                    procedimento_sigtap_codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    procedimento_texto = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    especialidade_texto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    unidade_executante_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_solicitante_id = table.Column<Guid>(type: "uuid", nullable: true),
                    solicitante_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    solicitante_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    solicitante_num_conselho = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    solicitante_uf_conselho = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    solicitante_cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    solicitante_conselho = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "CRM"),
                    codigo_solicitacao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    chave_confirmacao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    raw_sisreg = table.Column<string>(type: "text", nullable: true),
                    justificativa = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    observacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    prioridade = table.Column<int>(type: "integer", nullable: false),
                    data_solicitacao = table.Column<DateOnly>(type: "date", nullable: true),
                    data_regulacao = table.Column<DateOnly>(type: "date", nullable: true),
                    data_agendada = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status_confirmacao = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    confirmado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    confirmado_canal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    confirmacao_cancelada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    motivo_cancelamento_paciente = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    autorizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    autorizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo_cancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_solicitacao", x => x.id);
                    table.ForeignKey(
                        name: "FK_solicitacao_unidade_unidade_executante_id",
                        column: x => x.unidade_executante_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_solicitacao_unidade_unidade_solicitante_id",
                        column: x => x.unidade_solicitante_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_solicitacao_usuario_solicitante_usuario_id",
                        column: x => x.solicitante_usuario_id,
                        principalSchema: "smsmarica",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exame_imagem",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_exame_id = table.Column<Guid>(type: "uuid", nullable: true),
                    accession_number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    study_instance_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    worklist_item_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    iniciado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    realizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    data_estudo = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    erro_integracao_pacs = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    tentativas_envio = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ultima_tentativa_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    proxima_tentativa_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    imagens_preparadas_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    imagens_preparacao_tentativas = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
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
                    table.PrimaryKey("PK_exame_imagem", x => x.id);
                    table.ForeignKey(
                        name: "FK_exame_imagem_solicitacao_solicitacao_id",
                        column: x => x.solicitacao_id,
                        principalSchema: "smsmarica",
                        principalTable: "solicitacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_exame_imagem_tipo_exame_tipo_exame_id",
                        column: x => x.tipo_exame_id,
                        principalSchema: "smsmarica",
                        principalTable: "tipo_exame",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exame_imagem_accession_number",
                schema: "smsmarica",
                table: "exame_imagem",
                column: "accession_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exame_imagem_solicitacao_id",
                schema: "smsmarica",
                table: "exame_imagem",
                column: "solicitacao_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exame_imagem_status_proxima_tentativa_em",
                schema: "smsmarica",
                table: "exame_imagem",
                columns: new[] { "status", "proxima_tentativa_em" });

            migrationBuilder.CreateIndex(
                name: "IX_exame_imagem_study_instance_uid",
                schema: "smsmarica",
                table: "exame_imagem",
                column: "study_instance_uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exame_imagem_tipo_exame_id",
                schema: "smsmarica",
                table: "exame_imagem",
                column: "tipo_exame_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_categoria",
                schema: "smsmarica",
                table: "solicitacao",
                column: "categoria");

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_codigo_solicitacao",
                schema: "smsmarica",
                table: "solicitacao",
                column: "codigo_solicitacao",
                unique: true,
                filter: "codigo_solicitacao IS NOT NULL AND codigo_solicitacao <> '0000' AND excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_paciente_id",
                schema: "smsmarica",
                table: "solicitacao",
                column: "paciente_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_solicitante_usuario_id",
                schema: "smsmarica",
                table: "solicitacao",
                column: "solicitante_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_status",
                schema: "smsmarica",
                table: "solicitacao",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_status_data_agendada",
                schema: "smsmarica",
                table: "solicitacao",
                columns: new[] { "status", "data_agendada" });

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_unidade_executante_id",
                schema: "smsmarica",
                table: "solicitacao",
                column: "unidade_executante_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_unidade_solicitante_id",
                schema: "smsmarica",
                table: "solicitacao",
                column: "unidade_solicitante_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exame_imagem",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "solicitacao",
                schema: "smsmarica");
        }
    }
}
