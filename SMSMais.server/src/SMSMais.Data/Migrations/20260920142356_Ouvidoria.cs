using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class Ouvidoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "ouvidoria_protocolo_seq",
                schema: "smsmarica");

            migrationBuilder.CreateTable(
                name: "ouvidoria_assunto",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pai_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    codigo_ouvidor_sus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ouvidoria_assunto", x => x.id);
                    table.ForeignKey(
                        name: "FK_ouvidoria_assunto_ouvidoria_assunto_pai_id",
                        column: x => x.pai_id,
                        principalSchema: "smsmarica",
                        principalTable: "ouvidoria_assunto",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ouvidoria_configuracao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    prazo_cidadao_dias = table.Column<int>(type: "integer", nullable: false),
                    prorrogacao_dias = table.Column<int>(type: "integer", nullable: false),
                    prazo_area_dias = table.Column<int>(type: "integer", nullable: false),
                    prazo_area_alta_dias = table.Column<int>(type: "integer", nullable: false),
                    prazo_area_urgente_dias_uteis = table.Column<int>(type: "integer", nullable: false),
                    complementacao_dias = table.Column<int>(type: "integer", nullable: false),
                    arquivamento_automatico_dias = table.Column<int>(type: "integer", nullable: false),
                    notificar_por_whatsapp = table.Column<bool>(type: "boolean", nullable: false),
                    texto_recibo = table.Column<string>(type: "text", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ouvidoria_configuracao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ouvidoria_marcador",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ouvidoria_marcador", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ouvidoria_ponto_resposta",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prazo_dias = table.Column<int>(type: "integer", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_ouvidoria_ponto_resposta", x => x.id);
                    table.ForeignKey(
                        name: "FK_ouvidoria_ponto_resposta_unidade_unidade_id",
                        column: x => x.unidade_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ouvidoria_manifestacao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    protocolo = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    codigo_acesso_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    identificacao = table.Column<int>(type: "integer", nullable: false),
                    canal = table.Column<int>(type: "integer", nullable: false),
                    origem = table.Column<int>(type: "integer", nullable: false),
                    prioridade = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    assunto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subassunto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resumo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    teor = table.Column<string>(type: "text", nullable: false),
                    teor_pseudonimizado = table.Column<string>(type: "text", nullable: true),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ponto_resposta_id = table.Column<Guid>(type: "uuid", nullable: true),
                    regulacao_solicitacao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    protocolo_externo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    sistema_externo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    data_fato = table.Column<DateOnly>(type: "date", nullable: true),
                    local_fato = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    manifestante_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    manifestante_cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    manifestante_telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    manifestante_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    manifestante_patient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    referido_patient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    referido_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    referido_cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    referido_cns = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    envolvido_practitioner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    envolvido_descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    registrada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    prazo_resposta_em = table.Column<DateOnly>(type: "date", nullable: false),
                    prorrogado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    prorrogacao_justificativa = table.Column<string>(type: "text", nullable: true),
                    prazo_area_em = table.Column<DateOnly>(type: "date", nullable: true),
                    encaminhada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    complementacao_solicitada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    complementacao_usada = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    suspensa_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dias_suspensos = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    respondida_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    concluida_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dias_ate_resposta = table.Column<int>(type: "integer", nullable: true),
                    dias_atraso = table.Column<int>(type: "integer", nullable: true),
                    ultima_atividade_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    resolutividade = table.Column<int>(type: "integer", nullable: true),
                    situacao_final = table.Column<int>(type: "integer", nullable: true),
                    motivo_nao_atendimento = table.Column<int>(type: "integer", nullable: true),
                    motivo_arquivamento = table.Column<int>(type: "integer", nullable: true),
                    resposta_conclusiva = table.Column<string>(type: "text", nullable: true),
                    habilitada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    habilitada_por = table.Column<Guid>(type: "uuid", nullable: true),
                    responsavel_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_ouvidoria_manifestacao", x => x.id);
                    table.ForeignKey(
                        name: "FK_ouvidoria_manifestacao_ouvidoria_assunto_assunto_id",
                        column: x => x.assunto_id,
                        principalSchema: "smsmarica",
                        principalTable: "ouvidoria_assunto",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ouvidoria_manifestacao_ouvidoria_assunto_subassunto_id",
                        column: x => x.subassunto_id,
                        principalSchema: "smsmarica",
                        principalTable: "ouvidoria_assunto",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ouvidoria_manifestacao_ouvidoria_ponto_resposta_ponto_respo~",
                        column: x => x.ponto_resposta_id,
                        principalSchema: "smsmarica",
                        principalTable: "ouvidoria_ponto_resposta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ouvidoria_manifestacao_regulacao_solicitacao_regulacao_soli~",
                        column: x => x.regulacao_solicitacao_id,
                        principalSchema: "smsmarica",
                        principalTable: "regulacao_solicitacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ouvidoria_manifestacao_unidade_unidade_id",
                        column: x => x.unidade_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ouvidoria_ponto_resposta_membro",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ponto_resposta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titular = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ouvidoria_ponto_resposta_membro", x => x.id);
                    table.ForeignKey(
                        name: "FK_ouvidoria_ponto_resposta_membro_ouvidoria_ponto_resposta_po~",
                        column: x => x.ponto_resposta_id,
                        principalSchema: "smsmarica",
                        principalTable: "ouvidoria_ponto_resposta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ouvidoria_ponto_resposta_membro_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "smsmarica",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ouvidoria_acesso_identidade",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    manifestacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    justificativa = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ouvidoria_acesso_identidade", x => x.id);
                    table.ForeignKey(
                        name: "FK_ouvidoria_acesso_identidade_ouvidoria_manifestacao_manifest~",
                        column: x => x.manifestacao_id,
                        principalSchema: "smsmarica",
                        principalTable: "ouvidoria_manifestacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ouvidoria_acesso_identidade_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "smsmarica",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ouvidoria_evento",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    manifestacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    status_anterior = table.Column<int>(type: "integer", nullable: true),
                    status_novo = table.Column<int>(type: "integer", nullable: true),
                    autor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    autor_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ponto_resposta_id = table.Column<Guid>(type: "uuid", nullable: true),
                    texto = table.Column<string>(type: "text", nullable: true),
                    visivel_ao_cidadao = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ouvidoria_evento", x => x.id);
                    table.ForeignKey(
                        name: "FK_ouvidoria_evento_ouvidoria_manifestacao_manifestacao_id",
                        column: x => x.manifestacao_id,
                        principalSchema: "smsmarica",
                        principalTable: "ouvidoria_manifestacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ouvidoria_evento_ouvidoria_ponto_resposta_ponto_resposta_id",
                        column: x => x.ponto_resposta_id,
                        principalSchema: "smsmarica",
                        principalTable: "ouvidoria_ponto_resposta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ouvidoria_manifestacao_marcador",
                schema: "smsmarica",
                columns: table => new
                {
                    manifestacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    marcador_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ouvidoria_manifestacao_marcador", x => new { x.manifestacao_id, x.marcador_id });
                    table.ForeignKey(
                        name: "FK_ouvidoria_manifestacao_marcador_ouvidoria_manifestacao_mani~",
                        column: x => x.manifestacao_id,
                        principalSchema: "smsmarica",
                        principalTable: "ouvidoria_manifestacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ouvidoria_manifestacao_marcador_ouvidoria_marcador_marcador~",
                        column: x => x.marcador_id,
                        principalSchema: "smsmarica",
                        principalTable: "ouvidoria_marcador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ouvidoria_anexo",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    manifestacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    midia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_arquivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    visivel_ao_cidadao = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ouvidoria_anexo", x => x.id);
                    table.ForeignKey(
                        name: "FK_ouvidoria_anexo_midia_midia_id",
                        column: x => x.midia_id,
                        principalSchema: "smsmarica",
                        principalTable: "midia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ouvidoria_anexo_ouvidoria_evento_evento_id",
                        column: x => x.evento_id,
                        principalSchema: "smsmarica",
                        principalTable: "ouvidoria_evento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ouvidoria_anexo_ouvidoria_manifestacao_manifestacao_id",
                        column: x => x.manifestacao_id,
                        principalSchema: "smsmarica",
                        principalTable: "ouvidoria_manifestacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ouvidoria_acesso_identidade_manifestacao_id_criado_em",
                schema: "smsmarica",
                table: "ouvidoria_acesso_identidade",
                columns: new[] { "manifestacao_id", "criado_em" });

            migrationBuilder.CreateIndex(
                name: "IX_ouvidoria_acesso_identidade_usuario_id",
                schema: "smsmarica",
                table: "ouvidoria_acesso_identidade",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_ouvidoria_anexo_evento_id",
                schema: "smsmarica",
                table: "ouvidoria_anexo",
                column: "evento_id");

            migrationBuilder.CreateIndex(
                name: "IX_ouvidoria_anexo_manifestacao_id",
                schema: "smsmarica",
                table: "ouvidoria_anexo",
                column: "manifestacao_id");

            migrationBuilder.CreateIndex(
                name: "IX_ouvidoria_anexo_midia_id",
                schema: "smsmarica",
                table: "ouvidoria_anexo",
                column: "midia_id");

            migrationBuilder.CreateIndex(
                name: "IX_ouvidoria_assunto_pai_id_nome",
                schema: "smsmarica",
                table: "ouvidoria_assunto",
                columns: new[] { "pai_id", "nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ouvidoria_evento_manifestacao_id_criado_em",
                schema: "smsmarica",
                table: "ouvidoria_evento",
                columns: new[] { "manifestacao_id", "criado_em" });

            migrationBuilder.CreateIndex(
                name: "IX_ouvidoria_evento_ponto_resposta_id",
                schema: "smsmarica",
                table: "ouvidoria_evento",
                column: "ponto_resposta_id");

            migrationBuilder.CreateIndex(
                name: "IX_ouvidoria_manifestacao_assunto_id",
                schema: "smsmarica",
                table: "ouvidoria_manifestacao",
                column: "assunto_id");

            migrationBuilder.CreateIndex(
                name: "IX_ouvidoria_manifestacao_manifestante_cpf",
                schema: "smsmarica",
                table: "ouvidoria_manifestacao",
                column: "manifestante_cpf");

            migrationBuilder.CreateIndex(
                name: "IX_ouvidoria_manifestacao_ponto_resposta_id",
                schema: "smsmarica",
                table: "ouvidoria_manifestacao",
                column: "ponto_resposta_id");

            migrationBuilder.CreateIndex(
                name: "IX_ouvidoria_manifestacao_prazo_resposta_em",
                schema: "smsmarica",
                table: "ouvidoria_manifestacao",
                column: "prazo_resposta_em");

            migrationBuilder.CreateIndex(
                name: "IX_ouvidoria_manifestacao_protocolo",
                schema: "smsmarica",
                table: "ouvidoria_manifestacao",
                column: "protocolo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ouvidoria_manifestacao_regulacao_solicitacao_id",
                schema: "smsmarica",
                table: "ouvidoria_manifestacao",
                column: "regulacao_solicitacao_id");

            migrationBuilder.CreateIndex(
                name: "IX_ouvidoria_manifestacao_status",
                schema: "smsmarica",
                table: "ouvidoria_manifestacao",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_ouvidoria_manifestacao_subassunto_id",
                schema: "smsmarica",
                table: "ouvidoria_manifestacao",
                column: "subassunto_id");

            migrationBuilder.CreateIndex(
                name: "IX_ouvidoria_manifestacao_tipo_registrada_em",
                schema: "smsmarica",
                table: "ouvidoria_manifestacao",
                columns: new[] { "tipo", "registrada_em" });

            migrationBuilder.CreateIndex(
                name: "IX_ouvidoria_manifestacao_unidade_id",
                schema: "smsmarica",
                table: "ouvidoria_manifestacao",
                column: "unidade_id");

            migrationBuilder.CreateIndex(
                name: "IX_ouvidoria_manifestacao_marcador_marcador_id",
                schema: "smsmarica",
                table: "ouvidoria_manifestacao_marcador",
                column: "marcador_id");

            migrationBuilder.CreateIndex(
                name: "IX_ouvidoria_marcador_nome",
                schema: "smsmarica",
                table: "ouvidoria_marcador",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ouvidoria_ponto_resposta_unidade_id",
                schema: "smsmarica",
                table: "ouvidoria_ponto_resposta",
                column: "unidade_id",
                unique: true,
                filter: "unidade_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ouvidoria_ponto_resposta_membro_ponto_resposta_id_usuario_id",
                schema: "smsmarica",
                table: "ouvidoria_ponto_resposta_membro",
                columns: new[] { "ponto_resposta_id", "usuario_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ouvidoria_ponto_resposta_membro_usuario_id",
                schema: "smsmarica",
                table: "ouvidoria_ponto_resposta_membro",
                column: "usuario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ouvidoria_acesso_identidade",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ouvidoria_anexo",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ouvidoria_configuracao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ouvidoria_manifestacao_marcador",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ouvidoria_ponto_resposta_membro",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ouvidoria_evento",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ouvidoria_marcador",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ouvidoria_manifestacao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ouvidoria_assunto",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ouvidoria_ponto_resposta",
                schema: "smsmarica");

            migrationBuilder.DropSequence(
                name: "ouvidoria_protocolo_seq",
                schema: "smsmarica");
        }
    }
}
