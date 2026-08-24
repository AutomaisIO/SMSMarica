using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class ContractSolicitacaoExame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_anamnese_solicitacao_exame_solicitacao_exame_id",
                schema: "smsmarica",
                table: "anamnese");

            migrationBuilder.DropForeignKey(
                name: "FK_anexo_upload_token_solicitacao_exame_solicitacao_exame_id",
                schema: "smsmarica",
                table: "anexo_upload_token");

            migrationBuilder.DropForeignKey(
                name: "FK_comunicacao_paciente_solicitacao_exame_solicitacao_exame_id",
                schema: "smsmarica",
                table: "comunicacao_paciente");

            migrationBuilder.DropForeignKey(
                name: "FK_contato_registro_solicitacao_exame_solicitacao_exame_id",
                schema: "smsmarica",
                table: "contato_registro");

            migrationBuilder.DropForeignKey(
                name: "FK_declaracao_comparecimento_verificacao_solicitacao_exame_sol~",
                schema: "smsmarica",
                table: "declaracao_comparecimento_verificacao");

            migrationBuilder.DropForeignKey(
                name: "FK_documento_exame_solicitacao_exame_solicitacao_exame_id",
                schema: "smsmarica",
                table: "documento_exame");

            migrationBuilder.DropForeignKey(
                name: "FK_exame_associacao_solicitacao_exame_solicitacao_exame_id",
                schema: "smsmarica",
                table: "exame_associacao");

            migrationBuilder.DropTable(
                name: "solicitacao_exame",
                schema: "smsmarica");

            migrationBuilder.DropIndex(
                name: "IX_comunicacao_paciente_solicitacao_exame_id_finalidade",
                schema: "smsmarica",
                table: "comunicacao_paciente");

            migrationBuilder.RenameColumn(
                name: "solicitacao_exame_id",
                schema: "smsmarica",
                table: "exame_associacao",
                newName: "exame_imagem_id");

            migrationBuilder.RenameColumn(
                name: "solicitacao_exame_id",
                schema: "smsmarica",
                table: "documento_exame",
                newName: "exame_imagem_id");

            migrationBuilder.RenameIndex(
                name: "ix_documento_exame_solicitacao_exame_id",
                schema: "smsmarica",
                table: "documento_exame",
                newName: "ix_documento_exame_exame_imagem_id");

            migrationBuilder.RenameColumn(
                name: "solicitacao_exame_id",
                schema: "smsmarica",
                table: "declaracao_comparecimento_verificacao",
                newName: "exame_imagem_id");

            migrationBuilder.RenameIndex(
                name: "IX_declaracao_comparecimento_verificacao_solicitacao_exame_id",
                schema: "smsmarica",
                table: "declaracao_comparecimento_verificacao",
                newName: "IX_declaracao_comparecimento_verificacao_exame_imagem_id");

            migrationBuilder.RenameColumn(
                name: "solicitacao_exame_id",
                schema: "smsmarica",
                table: "contato_registro",
                newName: "solicitacao_id");

            migrationBuilder.RenameIndex(
                name: "IX_contato_registro_solicitacao_exame_id_criado_em",
                schema: "smsmarica",
                table: "contato_registro",
                newName: "IX_contato_registro_solicitacao_id_criado_em");

            migrationBuilder.RenameColumn(
                name: "solicitacao_exame_id",
                schema: "smsmarica",
                table: "comunicacao_paciente",
                newName: "solicitacao_id");

            migrationBuilder.RenameColumn(
                name: "solicitacao_exame_id",
                schema: "smsmarica",
                table: "cidadao_login_link",
                newName: "solicitacao_id");

            migrationBuilder.RenameColumn(
                name: "solicitacao_exame_id",
                schema: "smsmarica",
                table: "anexo_upload_token",
                newName: "exame_imagem_id");

            migrationBuilder.RenameIndex(
                name: "IX_anexo_upload_token_solicitacao_exame_id",
                schema: "smsmarica",
                table: "anexo_upload_token",
                newName: "IX_anexo_upload_token_exame_imagem_id");

            migrationBuilder.RenameColumn(
                name: "solicitacao_exame_id",
                schema: "smsmarica",
                table: "anamnese",
                newName: "exame_imagem_id");

            migrationBuilder.RenameIndex(
                name: "ix_anamnese_solicitacao_exame_id_ativa",
                schema: "smsmarica",
                table: "anamnese",
                newName: "ix_anamnese_exame_imagem_id_ativa");

            migrationBuilder.CreateIndex(
                name: "IX_comunicacao_paciente_solicitacao_id_finalidade",
                schema: "smsmarica",
                table: "comunicacao_paciente",
                columns: new[] { "solicitacao_id", "finalidade" },
                unique: true,
                filter: "solicitacao_id IS NOT NULL");

            // FKs de REGULAÇÃO: a coluna foi renomeada para solicitacao_id mas ainda carrega o id
            // ANTIGO do exame (= exame_imagem.id, preservado na migração de dados). Traduz para o id
            // da ESPINHA antes de criar a FK para solicitacao (senão viola a FK). Ver ADR-0021.
            // exame_imagem já está populado (migração MigrarDadosSolicitacaoExame). Nulos (login_link
            // avulso) não casam com ei.id e ficam intactos.
            migrationBuilder.Sql(@"
UPDATE smsmarica.comunicacao_paciente c
   SET solicitacao_id = ei.solicitacao_id
  FROM smsmarica.exame_imagem ei
 WHERE ei.id = c.solicitacao_id;

UPDATE smsmarica.contato_registro c
   SET solicitacao_id = ei.solicitacao_id
  FROM smsmarica.exame_imagem ei
 WHERE ei.id = c.solicitacao_id;

UPDATE smsmarica.cidadao_login_link c
   SET solicitacao_id = ei.solicitacao_id
  FROM smsmarica.exame_imagem ei
 WHERE ei.id = c.solicitacao_id;
");

            migrationBuilder.AddForeignKey(
                name: "FK_anamnese_exame_imagem_exame_imagem_id",
                schema: "smsmarica",
                table: "anamnese",
                column: "exame_imagem_id",
                principalSchema: "smsmarica",
                principalTable: "exame_imagem",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_anexo_upload_token_exame_imagem_exame_imagem_id",
                schema: "smsmarica",
                table: "anexo_upload_token",
                column: "exame_imagem_id",
                principalSchema: "smsmarica",
                principalTable: "exame_imagem",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_comunicacao_paciente_solicitacao_solicitacao_id",
                schema: "smsmarica",
                table: "comunicacao_paciente",
                column: "solicitacao_id",
                principalSchema: "smsmarica",
                principalTable: "solicitacao",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_contato_registro_solicitacao_solicitacao_id",
                schema: "smsmarica",
                table: "contato_registro",
                column: "solicitacao_id",
                principalSchema: "smsmarica",
                principalTable: "solicitacao",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_declaracao_comparecimento_verificacao_exame_imagem_exame_im~",
                schema: "smsmarica",
                table: "declaracao_comparecimento_verificacao",
                column: "exame_imagem_id",
                principalSchema: "smsmarica",
                principalTable: "exame_imagem",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_documento_exame_exame_imagem_exame_imagem_id",
                schema: "smsmarica",
                table: "documento_exame",
                column: "exame_imagem_id",
                principalSchema: "smsmarica",
                principalTable: "exame_imagem",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_exame_associacao_exame_imagem_exame_imagem_id",
                schema: "smsmarica",
                table: "exame_associacao",
                column: "exame_imagem_id",
                principalSchema: "smsmarica",
                principalTable: "exame_imagem",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_anamnese_exame_imagem_exame_imagem_id",
                schema: "smsmarica",
                table: "anamnese");

            migrationBuilder.DropForeignKey(
                name: "FK_anexo_upload_token_exame_imagem_exame_imagem_id",
                schema: "smsmarica",
                table: "anexo_upload_token");

            migrationBuilder.DropForeignKey(
                name: "FK_comunicacao_paciente_solicitacao_solicitacao_id",
                schema: "smsmarica",
                table: "comunicacao_paciente");

            migrationBuilder.DropForeignKey(
                name: "FK_contato_registro_solicitacao_solicitacao_id",
                schema: "smsmarica",
                table: "contato_registro");

            migrationBuilder.DropForeignKey(
                name: "FK_declaracao_comparecimento_verificacao_exame_imagem_exame_im~",
                schema: "smsmarica",
                table: "declaracao_comparecimento_verificacao");

            migrationBuilder.DropForeignKey(
                name: "FK_documento_exame_exame_imagem_exame_imagem_id",
                schema: "smsmarica",
                table: "documento_exame");

            migrationBuilder.DropForeignKey(
                name: "FK_exame_associacao_exame_imagem_exame_imagem_id",
                schema: "smsmarica",
                table: "exame_associacao");

            migrationBuilder.DropIndex(
                name: "IX_comunicacao_paciente_solicitacao_id_finalidade",
                schema: "smsmarica",
                table: "comunicacao_paciente");

            migrationBuilder.RenameColumn(
                name: "exame_imagem_id",
                schema: "smsmarica",
                table: "exame_associacao",
                newName: "solicitacao_exame_id");

            migrationBuilder.RenameColumn(
                name: "exame_imagem_id",
                schema: "smsmarica",
                table: "documento_exame",
                newName: "solicitacao_exame_id");

            migrationBuilder.RenameIndex(
                name: "ix_documento_exame_exame_imagem_id",
                schema: "smsmarica",
                table: "documento_exame",
                newName: "ix_documento_exame_solicitacao_exame_id");

            migrationBuilder.RenameColumn(
                name: "exame_imagem_id",
                schema: "smsmarica",
                table: "declaracao_comparecimento_verificacao",
                newName: "solicitacao_exame_id");

            migrationBuilder.RenameIndex(
                name: "IX_declaracao_comparecimento_verificacao_exame_imagem_id",
                schema: "smsmarica",
                table: "declaracao_comparecimento_verificacao",
                newName: "IX_declaracao_comparecimento_verificacao_solicitacao_exame_id");

            migrationBuilder.RenameColumn(
                name: "solicitacao_id",
                schema: "smsmarica",
                table: "contato_registro",
                newName: "solicitacao_exame_id");

            migrationBuilder.RenameIndex(
                name: "IX_contato_registro_solicitacao_id_criado_em",
                schema: "smsmarica",
                table: "contato_registro",
                newName: "IX_contato_registro_solicitacao_exame_id_criado_em");

            migrationBuilder.RenameColumn(
                name: "solicitacao_id",
                schema: "smsmarica",
                table: "comunicacao_paciente",
                newName: "solicitacao_exame_id");

            migrationBuilder.RenameColumn(
                name: "solicitacao_id",
                schema: "smsmarica",
                table: "cidadao_login_link",
                newName: "solicitacao_exame_id");

            migrationBuilder.RenameColumn(
                name: "exame_imagem_id",
                schema: "smsmarica",
                table: "anexo_upload_token",
                newName: "solicitacao_exame_id");

            migrationBuilder.RenameIndex(
                name: "IX_anexo_upload_token_exame_imagem_id",
                schema: "smsmarica",
                table: "anexo_upload_token",
                newName: "IX_anexo_upload_token_solicitacao_exame_id");

            migrationBuilder.RenameColumn(
                name: "exame_imagem_id",
                schema: "smsmarica",
                table: "anamnese",
                newName: "solicitacao_exame_id");

            migrationBuilder.RenameIndex(
                name: "ix_anamnese_exame_imagem_id_ativa",
                schema: "smsmarica",
                table: "anamnese",
                newName: "ix_anamnese_solicitacao_exame_id_ativa");

            migrationBuilder.CreateTable(
                name: "solicitacao_exame",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitante_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo_exame_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_solicitante_id = table.Column<Guid>(type: "uuid", nullable: true),
                    accession_number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    autorizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    autorizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    chave_confirmacao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    codigo_solicitacao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    confirmacao_cancelada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    confirmado_canal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    confirmado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    data_agendada = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    data_estudo = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    data_regulacao = table.Column<DateOnly>(type: "date", nullable: true),
                    data_solicitacao = table.Column<DateOnly>(type: "date", nullable: true),
                    erro_integracao_pacs = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true),
                    imagens_preparacao_tentativas = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    imagens_preparadas_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    iniciado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    justificativa = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    motivo_cancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    motivo_cancelamento_paciente = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    observacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prioridade = table.Column<int>(type: "integer", nullable: false),
                    proxima_tentativa_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    raw_sisreg = table.Column<string>(type: "text", nullable: true),
                    realizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    solicitante_conselho = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "CRM"),
                    solicitante_cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    solicitante_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    solicitante_num_conselho = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    solicitante_uf_conselho = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    status_confirmacao = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    study_instance_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    tentativas_envio = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ultima_tentativa_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    worklist_item_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_solicitacao_exame", x => x.id);
                    table.ForeignKey(
                        name: "FK_solicitacao_exame_tipo_exame_tipo_exame_id",
                        column: x => x.tipo_exame_id,
                        principalSchema: "smsmarica",
                        principalTable: "tipo_exame",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_solicitacao_exame_unidade_unidade_id",
                        column: x => x.unidade_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_solicitacao_exame_unidade_unidade_solicitante_id",
                        column: x => x.unidade_solicitante_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_solicitacao_exame_usuario_solicitante_usuario_id",
                        column: x => x.solicitante_usuario_id,
                        principalSchema: "smsmarica",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_comunicacao_paciente_solicitacao_exame_id_finalidade",
                schema: "smsmarica",
                table: "comunicacao_paciente",
                columns: new[] { "solicitacao_exame_id", "finalidade" },
                unique: true,
                filter: "solicitacao_exame_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_exame_accession_number",
                schema: "smsmarica",
                table: "solicitacao_exame",
                column: "accession_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_exame_codigo_solicitacao",
                schema: "smsmarica",
                table: "solicitacao_exame",
                column: "codigo_solicitacao",
                unique: true,
                filter: "codigo_solicitacao IS NOT NULL AND codigo_solicitacao <> '0000' AND excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_exame_paciente_id",
                schema: "smsmarica",
                table: "solicitacao_exame",
                column: "paciente_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_exame_solicitante_usuario_id",
                schema: "smsmarica",
                table: "solicitacao_exame",
                column: "solicitante_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_exame_status",
                schema: "smsmarica",
                table: "solicitacao_exame",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_exame_status_data_agendada",
                schema: "smsmarica",
                table: "solicitacao_exame",
                columns: new[] { "status", "data_agendada" });

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_exame_status_proxima_tentativa_em",
                schema: "smsmarica",
                table: "solicitacao_exame",
                columns: new[] { "status", "proxima_tentativa_em" });

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_exame_study_instance_uid",
                schema: "smsmarica",
                table: "solicitacao_exame",
                column: "study_instance_uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_exame_tipo_exame_id",
                schema: "smsmarica",
                table: "solicitacao_exame",
                column: "tipo_exame_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_exame_unidade_id",
                schema: "smsmarica",
                table: "solicitacao_exame",
                column: "unidade_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_exame_unidade_solicitante_id",
                schema: "smsmarica",
                table: "solicitacao_exame",
                column: "unidade_solicitante_id");

            migrationBuilder.AddForeignKey(
                name: "FK_anamnese_solicitacao_exame_solicitacao_exame_id",
                schema: "smsmarica",
                table: "anamnese",
                column: "solicitacao_exame_id",
                principalSchema: "smsmarica",
                principalTable: "solicitacao_exame",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_anexo_upload_token_solicitacao_exame_solicitacao_exame_id",
                schema: "smsmarica",
                table: "anexo_upload_token",
                column: "solicitacao_exame_id",
                principalSchema: "smsmarica",
                principalTable: "solicitacao_exame",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_comunicacao_paciente_solicitacao_exame_solicitacao_exame_id",
                schema: "smsmarica",
                table: "comunicacao_paciente",
                column: "solicitacao_exame_id",
                principalSchema: "smsmarica",
                principalTable: "solicitacao_exame",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_contato_registro_solicitacao_exame_solicitacao_exame_id",
                schema: "smsmarica",
                table: "contato_registro",
                column: "solicitacao_exame_id",
                principalSchema: "smsmarica",
                principalTable: "solicitacao_exame",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_declaracao_comparecimento_verificacao_solicitacao_exame_sol~",
                schema: "smsmarica",
                table: "declaracao_comparecimento_verificacao",
                column: "solicitacao_exame_id",
                principalSchema: "smsmarica",
                principalTable: "solicitacao_exame",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_documento_exame_solicitacao_exame_solicitacao_exame_id",
                schema: "smsmarica",
                table: "documento_exame",
                column: "solicitacao_exame_id",
                principalSchema: "smsmarica",
                principalTable: "solicitacao_exame",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_exame_associacao_solicitacao_exame_solicitacao_exame_id",
                schema: "smsmarica",
                table: "exame_associacao",
                column: "solicitacao_exame_id",
                principalSchema: "smsmarica",
                principalTable: "solicitacao_exame",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
