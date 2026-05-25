using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSolicitacoesExame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "procedimento_sigtap",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    grupo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    subgrupo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    forma = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    competencia_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    competencia_fim = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_procedimento_sigtap", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tipo_exame",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    procedimento_sigtap_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modalidade_dicom = table.Column<int>(type: "integer", nullable: false),
                    requested_procedure_description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    scheduled_procedure_step_description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    codigos_protocolo = table.Column<List<string>>(type: "text[]", nullable: false),
                    tempo_estimado_minutos = table.Column<int>(type: "integer", nullable: true),
                    unidade_padrao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tipo_exame", x => x.id);
                    table.ForeignKey(
                        name: "FK_tipo_exame_procedimento_sigtap_procedimento_sigtap_id",
                        column: x => x.procedimento_sigtap_id,
                        principalSchema: "smsmarica",
                        principalTable: "procedimento_sigtap",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tipo_exame_unidade_unidade_padrao_id",
                        column: x => x.unidade_padrao_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "solicitacao_exame",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    accession_number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    study_instance_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    worklist_item_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_exame_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitante_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    solicitante_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    solicitante_crm = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    solicitante_uf_crm = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    numero_regulacao_sus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    justificativa = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    prioridade = table.Column<int>(type: "integer", nullable: false),
                    observacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    data_agendada = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    iniciado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    realizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    erro_integracao_pacs = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_solicitacao_exame", x => x.id);
                    table.ForeignKey(
                        name: "FK_solicitacao_exame_paciente_paciente_id",
                        column: x => x.paciente_id,
                        principalSchema: "smsmarica",
                        principalTable: "paciente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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
                        name: "FK_solicitacao_exame_usuario_solicitante_usuario_id",
                        column: x => x.solicitante_usuario_id,
                        principalSchema: "smsmarica",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "smsmarica",
                table: "procedimento_sigtap",
                columns: new[] { "id", "ativo", "codigo", "competencia_fim", "competencia_inicio", "descricao", "forma", "grupo", "nome", "subgrupo" },
                values: new object[,]
                {
                    { new Guid("a1000000-0000-0000-0000-000000000001"), true, "02.04.03.018-8", null, new DateOnly(2025, 1, 1), "MAMOGRAFIA BILATERAL PARA RASTREAMENTO", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "MAMOGRAFIA BILATERAL PARA RASTREAMENTO", "DIAGNOSTICO POR RADIOLOGIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000002"), true, "02.04.03.003-0", null, new DateOnly(2025, 1, 1), "MAMOGRAFIA BILATERAL", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "MAMOGRAFIA BILATERAL", "DIAGNOSTICO POR RADIOLOGIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000003"), true, "02.04.03.001-3", null, new DateOnly(2025, 1, 1), "MAMOGRAFIA UNILATERAL", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "MAMOGRAFIA UNILATERAL", "DIAGNOSTICO POR RADIOLOGIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000010"), true, "02.04.01.004-7", null, new DateOnly(2025, 1, 1), "RADIOGRAFIA DE TORAX (PA)", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "RADIOGRAFIA DE TORAX (PA)", "DIAGNOSTICO POR RADIOLOGIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000011"), true, "02.04.01.005-5", null, new DateOnly(2025, 1, 1), "RADIOGRAFIA DE TORAX (PA E PERFIL)", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "RADIOGRAFIA DE TORAX (PA E PERFIL)", "DIAGNOSTICO POR RADIOLOGIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000012"), true, "02.04.05.004-2", null, new DateOnly(2025, 1, 1), "RADIOGRAFIA DA COLUNA LOMBO-SACRA", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "RADIOGRAFIA DA COLUNA LOMBO-SACRA", "DIAGNOSTICO POR RADIOLOGIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000013"), true, "02.04.05.002-6", null, new DateOnly(2025, 1, 1), "RADIOGRAFIA DA COLUNA CERVICAL", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "RADIOGRAFIA DA COLUNA CERVICAL", "DIAGNOSTICO POR RADIOLOGIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000014"), true, "02.04.05.003-4", null, new DateOnly(2025, 1, 1), "RADIOGRAFIA DA COLUNA TORACICA", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "RADIOGRAFIA DA COLUNA TORACICA", "DIAGNOSTICO POR RADIOLOGIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000015"), true, "02.04.04.028-3", null, new DateOnly(2025, 1, 1), "RADIOGRAFIA DE JOELHO (AP + LATERAL)", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "RADIOGRAFIA DE JOELHO (AP + LATERAL)", "DIAGNOSTICO POR RADIOLOGIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000016"), true, "02.04.04.030-5", null, new DateOnly(2025, 1, 1), "RADIOGRAFIA DE OMBRO (AP + AXIAL)", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "RADIOGRAFIA DE OMBRO (AP + AXIAL)", "DIAGNOSTICO POR RADIOLOGIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000017"), true, "02.04.04.020-8", null, new DateOnly(2025, 1, 1), "RADIOGRAFIA DE BACIA", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "RADIOGRAFIA DE BACIA", "DIAGNOSTICO POR RADIOLOGIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000018"), true, "02.04.06.016-3", null, new DateOnly(2025, 1, 1), "RADIOGRAFIA DE ABDOME (AGUDO ADULTO 3 INCIDENCIAS)", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "RADIOGRAFIA DE ABDOME (AGUDO ADULTO 3 INCIDENCIAS)", "DIAGNOSTICO POR RADIOLOGIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000019"), true, "02.04.06.019-8", null, new DateOnly(2025, 1, 1), "RADIOGRAFIA DE ABDOME EM 1 INCIDENCIA", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "RADIOGRAFIA DE ABDOME EM 1 INCIDENCIA", "DIAGNOSTICO POR RADIOLOGIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000020"), true, "02.05.02.014-3", null, new DateOnly(2025, 1, 1), "ULTRASSONOGRAFIA DE ABDOME TOTAL", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "ULTRASSONOGRAFIA DE ABDOME TOTAL", "DIAGNOSTICO POR ULTRASSONOGRAFIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000021"), true, "02.05.02.017-8", null, new DateOnly(2025, 1, 1), "ULTRASSONOGRAFIA DE TIREOIDE", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "ULTRASSONOGRAFIA DE TIREOIDE", "DIAGNOSTICO POR ULTRASSONOGRAFIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000022"), true, "02.05.02.010-0", null, new DateOnly(2025, 1, 1), "ULTRASSONOGRAFIA OBSTETRICA", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "ULTRASSONOGRAFIA OBSTETRICA", "DIAGNOSTICO POR ULTRASSONOGRAFIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000023"), true, "02.05.02.013-5", null, new DateOnly(2025, 1, 1), "ULTRASSONOGRAFIA TRANSVAGINAL", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "ULTRASSONOGRAFIA TRANSVAGINAL", "DIAGNOSTICO POR ULTRASSONOGRAFIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000024"), true, "02.05.02.015-1", null, new DateOnly(2025, 1, 1), "ULTRASSONOGRAFIA MORFOLOGICA DO 1 TRIMESTRE", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "ULTRASSONOGRAFIA MORFOLOGICA DO 1 TRIMESTRE", "DIAGNOSTICO POR ULTRASSONOGRAFIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000025"), true, "02.05.02.005-4", null, new DateOnly(2025, 1, 1), "ULTRASSONOGRAFIA MAMARIA BILATERAL", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "ULTRASSONOGRAFIA MAMARIA BILATERAL", "DIAGNOSTICO POR ULTRASSONOGRAFIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000026"), true, "02.05.02.012-7", null, new DateOnly(2025, 1, 1), "ULTRASSONOGRAFIA PELVICA (GINECOLOGICA)", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "ULTRASSONOGRAFIA PELVICA (GINECOLOGICA)", "DIAGNOSTICO POR ULTRASSONOGRAFIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000030"), true, "02.06.01.002-8", null, new DateOnly(2025, 1, 1), "TOMOGRAFIA COMPUTADORIZADA DO CRANIO", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "TOMOGRAFIA COMPUTADORIZADA DO CRANIO", "TOMOGRAFIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000031"), true, "02.06.02.003-1", null, new DateOnly(2025, 1, 1), "TOMOGRAFIA COMPUTADORIZADA DE TORAX", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "TOMOGRAFIA COMPUTADORIZADA DE TORAX", "TOMOGRAFIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000032"), true, "02.06.02.002-3", null, new DateOnly(2025, 1, 1), "TOMOGRAFIA COMPUTADORIZADA DE ABDOME SUPERIOR", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "TOMOGRAFIA COMPUTADORIZADA DE ABDOME SUPERIOR", "TOMOGRAFIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000033"), true, "02.06.03.001-0", null, new DateOnly(2025, 1, 1), "TOMOGRAFIA COMPUTADORIZADA DA COLUNA LOMBAR", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "TOMOGRAFIA COMPUTADORIZADA DA COLUNA LOMBAR", "TOMOGRAFIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000034"), true, "02.06.03.002-9", null, new DateOnly(2025, 1, 1), "TOMOGRAFIA COMPUTADORIZADA DA COLUNA CERVICAL", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "TOMOGRAFIA COMPUTADORIZADA DA COLUNA CERVICAL", "TOMOGRAFIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000040"), true, "02.07.01.001-3", null, new DateOnly(2025, 1, 1), "RESSONANCIA MAGNETICA DE CRANIO", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "RESSONANCIA MAGNETICA DE CRANIO", "RESSONANCIA MAGNETICA" },
                    { new Guid("a1000000-0000-0000-0000-000000000041"), true, "02.07.01.003-0", null, new DateOnly(2025, 1, 1), "RESSONANCIA MAGNETICA DE COLUNA LOMBO-SACRA", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "RESSONANCIA MAGNETICA DE COLUNA LOMBO-SACRA", "RESSONANCIA MAGNETICA" },
                    { new Guid("a1000000-0000-0000-0000-000000000042"), true, "02.07.01.004-8", null, new DateOnly(2025, 1, 1), "RESSONANCIA MAGNETICA DE JOELHO", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "RESSONANCIA MAGNETICA DE JOELHO", "RESSONANCIA MAGNETICA" },
                    { new Guid("a1000000-0000-0000-0000-000000000043"), true, "02.07.01.002-1", null, new DateOnly(2025, 1, 1), "RESSONANCIA MAGNETICA DE COLUNA CERVICAL", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "RESSONANCIA MAGNETICA DE COLUNA CERVICAL", "RESSONANCIA MAGNETICA" },
                    { new Guid("a1000000-0000-0000-0000-000000000044"), true, "02.07.01.005-6", null, new DateOnly(2025, 1, 1), "RESSONANCIA MAGNETICA DE OMBRO", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "RESSONANCIA MAGNETICA DE OMBRO", "RESSONANCIA MAGNETICA" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_procedimento_sigtap_ativo",
                schema: "smsmarica",
                table: "procedimento_sigtap",
                column: "ativo");

            migrationBuilder.CreateIndex(
                name: "IX_procedimento_sigtap_codigo",
                schema: "smsmarica",
                table: "procedimento_sigtap",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_procedimento_sigtap_grupo_subgrupo",
                schema: "smsmarica",
                table: "procedimento_sigtap",
                columns: new[] { "grupo", "subgrupo" });

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_exame_accession_number",
                schema: "smsmarica",
                table: "solicitacao_exame",
                column: "accession_number",
                unique: true);

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
                name: "IX_tipo_exame_ativo",
                schema: "smsmarica",
                table: "tipo_exame",
                column: "ativo");

            migrationBuilder.CreateIndex(
                name: "IX_tipo_exame_modalidade_dicom",
                schema: "smsmarica",
                table: "tipo_exame",
                column: "modalidade_dicom");

            migrationBuilder.CreateIndex(
                name: "IX_tipo_exame_nome",
                schema: "smsmarica",
                table: "tipo_exame",
                column: "nome",
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tipo_exame_procedimento_sigtap_id",
                schema: "smsmarica",
                table: "tipo_exame",
                column: "procedimento_sigtap_id");

            migrationBuilder.CreateIndex(
                name: "IX_tipo_exame_unidade_padrao_id",
                schema: "smsmarica",
                table: "tipo_exame",
                column: "unidade_padrao_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "solicitacao_exame",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "tipo_exame",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "procedimento_sigtap",
                schema: "smsmarica");
        }
    }
}
