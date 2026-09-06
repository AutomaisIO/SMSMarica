using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class SolicitacaoDaRegulacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "regulacao_formulario_versao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    esquema = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    procedimento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    definicao_json = table.Column<string>(type: "jsonb", nullable: false),
                    hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulacao_formulario_versao", x => x.id);
                    table.ForeignKey(
                        name: "FK_regulacao_formulario_versao_regulacao_procedimento_procedim~",
                        column: x => x.procedimento_id,
                        principalSchema: "smsmarica",
                        principalTable: "regulacao_procedimento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "regulacao_formulario_campo_mapa",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    formulario_versao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chave_canonica = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sistema = table.Column<int>(type: "integer", nullable: false),
                    nome_nativo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    transformacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulacao_formulario_campo_mapa", x => x.id);
                    table.ForeignKey(
                        name: "FK_regulacao_formulario_campo_mapa_regulacao_formulario_versao~",
                        column: x => x.formulario_versao_id,
                        principalSchema: "smsmarica",
                        principalTable: "regulacao_formulario_versao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "regulacao_solicitacao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_local = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    fluxo = table.Column<int>(type: "integer", nullable: false),
                    unidade_solicitante_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_em_nome_de_id = table.Column<Guid>(type: "uuid", nullable: true),
                    criado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paciente_cpf = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    paciente_cns = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    paciente_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    procedimento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sistema_destino = table.Column<int>(type: "integer", nullable: true),
                    formulario_json = table.Column<string>(type: "jsonb", nullable: false),
                    formulario_versao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    status_motivo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    agente_responsavel_id = table.Column<Guid>(type: "uuid", nullable: true),
                    numero_externo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    enviado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    enviado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    credencial_usada_id = table.Column<Guid>(type: "uuid", nullable: true),
                    operador_externo_login = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    envio_assistido = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    solicitacao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ser_solicitacao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sernit_solicitacao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sisreg_editavel_ate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    origem_legado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    observacoes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_regulacao_solicitacao", x => x.id);
                    table.ForeignKey(
                        name: "FK_regulacao_solicitacao_regulacao_formulario_versao_formulari~",
                        column: x => x.formulario_versao_id,
                        principalSchema: "smsmarica",
                        principalTable: "regulacao_formulario_versao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_regulacao_solicitacao_regulacao_procedimento_procedimento_id",
                        column: x => x.procedimento_id,
                        principalSchema: "smsmarica",
                        principalTable: "regulacao_procedimento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_regulacao_solicitacao_unidade_unidade_em_nome_de_id",
                        column: x => x.unidade_em_nome_de_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_regulacao_solicitacao_unidade_unidade_solicitante_id",
                        column: x => x.unidade_solicitante_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "regulacao_solicitacao_exigencia",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    regra_id = table.Column<Guid>(type: "uuid", nullable: true),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    obrigatoria = table.Column<bool>(type: "boolean", nullable: false),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    exame_interno_exame_imagem_id = table.Column<Guid>(type: "uuid", nullable: true),
                    exame_interno_laudo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    validado_exame_por = table.Column<Guid>(type: "uuid", nullable: true),
                    validado_exame_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    critica_texto = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulacao_solicitacao_exigencia", x => x.id);
                    table.ForeignKey(
                        name: "FK_regulacao_solicitacao_exigencia_regulacao_solicitacao_solic~",
                        column: x => x.solicitacao_id,
                        principalSchema: "smsmarica",
                        principalTable: "regulacao_solicitacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "regulacao_exigencia_arquivo",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exigencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chave_armazenamento = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    nome = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    content_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tamanho = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    substitui_arquivo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    origem = table.Column<int>(type: "integer", nullable: false),
                    enviado_ao_sistema_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulacao_exigencia_arquivo", x => x.id);
                    table.ForeignKey(
                        name: "FK_regulacao_exigencia_arquivo_regulacao_exigencia_arquivo_sub~",
                        column: x => x.substitui_arquivo_id,
                        principalSchema: "smsmarica",
                        principalTable: "regulacao_exigencia_arquivo",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_regulacao_exigencia_arquivo_regulacao_solicitacao_exigencia~",
                        column: x => x.exigencia_id,
                        principalSchema: "smsmarica",
                        principalTable: "regulacao_solicitacao_exigencia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_regulacao_exig_arquivo",
                schema: "smsmarica",
                table: "regulacao_exigencia_arquivo",
                columns: new[] { "exigencia_id", "versao" });

            migrationBuilder.CreateIndex(
                name: "IX_regulacao_exigencia_arquivo_substitui_arquivo_id",
                schema: "smsmarica",
                table: "regulacao_exigencia_arquivo",
                column: "substitui_arquivo_id");

            migrationBuilder.CreateIndex(
                name: "ux_regulacao_form_mapa",
                schema: "smsmarica",
                table: "regulacao_formulario_campo_mapa",
                columns: new[] { "formulario_versao_id", "chave_canonica", "sistema" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_regulacao_form_versao_procedimento",
                schema: "smsmarica",
                table: "regulacao_formulario_versao",
                column: "procedimento_id");

            migrationBuilder.CreateIndex(
                name: "ux_regulacao_form_versao_hash",
                schema: "smsmarica",
                table: "regulacao_formulario_versao",
                column: "hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_regulacao_solicitacao_excluido_em",
                schema: "smsmarica",
                table: "regulacao_solicitacao",
                column: "excluido_em");

            migrationBuilder.CreateIndex(
                name: "IX_regulacao_solicitacao_formulario_versao_id",
                schema: "smsmarica",
                table: "regulacao_solicitacao",
                column: "formulario_versao_id");

            migrationBuilder.CreateIndex(
                name: "ix_regulacao_solicitacao_paciente",
                schema: "smsmarica",
                table: "regulacao_solicitacao",
                column: "paciente_id");

            migrationBuilder.CreateIndex(
                name: "IX_regulacao_solicitacao_procedimento_id",
                schema: "smsmarica",
                table: "regulacao_solicitacao",
                column: "procedimento_id");

            migrationBuilder.CreateIndex(
                name: "ix_regulacao_solicitacao_status",
                schema: "smsmarica",
                table: "regulacao_solicitacao",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_regulacao_solicitacao_unidade_em_nome_de_id",
                schema: "smsmarica",
                table: "regulacao_solicitacao",
                column: "unidade_em_nome_de_id");

            migrationBuilder.CreateIndex(
                name: "ix_regulacao_solicitacao_unidade_status",
                schema: "smsmarica",
                table: "regulacao_solicitacao",
                columns: new[] { "unidade_solicitante_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_regulacao_solicitacao_numero",
                schema: "smsmarica",
                table: "regulacao_solicitacao",
                column: "numero_local",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_regulacao_solicitacao_numero_externo",
                schema: "smsmarica",
                table: "regulacao_solicitacao",
                columns: new[] { "sistema_destino", "numero_externo" },
                unique: true,
                filter: "numero_externo IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_regulacao_exigencia_solicitacao",
                schema: "smsmarica",
                table: "regulacao_solicitacao_exigencia",
                column: "solicitacao_id");

            migrationBuilder.CreateIndex(
                name: "ux_regulacao_exigencia_regra",
                schema: "smsmarica",
                table: "regulacao_solicitacao_exigencia",
                columns: new[] { "solicitacao_id", "regra_id" },
                unique: true,
                filter: "regra_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "regulacao_exigencia_arquivo",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "regulacao_formulario_campo_mapa",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "regulacao_solicitacao_exigencia",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "regulacao_solicitacao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "regulacao_formulario_versao",
                schema: "smsmarica");
        }
    }
}
