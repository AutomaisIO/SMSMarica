using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLaudos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "laudo_template",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    categoria = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    conteudo_json = table.Column<string>(type: "jsonb", nullable: false),
                    conteudo_html = table.Column<string>(type: "text", nullable: false),
                    criado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    atualizado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_laudo_template", x => x.id);
                    table.ForeignKey(
                        name: "FK_laudo_template_usuario_atualizado_por_usuario_id",
                        column: x => x.atualizado_por_usuario_id,
                        principalSchema: "smsmarica",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_laudo_template_usuario_criado_por_usuario_id",
                        column: x => x.criado_por_usuario_id,
                        principalSchema: "smsmarica",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "laudo",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    study_instance_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    laudo_anterior_id = table.Column<Guid>(type: "uuid", nullable: true),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    medico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    laudo_template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    conteudo_json = table.Column<string>(type: "jsonb", nullable: false),
                    conteudo_html = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    medico_nome_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    medico_crm_snapshot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    medico_uf_crm_snapshot = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    medico_rqe_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    finalizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    excluido = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    excluido_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_laudo", x => x.id);
                    table.ForeignKey(
                        name: "FK_laudo_laudo_laudo_anterior_id",
                        column: x => x.laudo_anterior_id,
                        principalSchema: "smsmarica",
                        principalTable: "laudo",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_laudo_laudo_template_laudo_template_id",
                        column: x => x.laudo_template_id,
                        principalSchema: "smsmarica",
                        principalTable: "laudo_template",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_laudo_medico_medico_id",
                        column: x => x.medico_id,
                        principalSchema: "smsmarica",
                        principalTable: "medico",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_laudo_paciente_paciente_id",
                        column: x => x.paciente_id,
                        principalSchema: "smsmarica",
                        principalTable: "paciente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_laudo_laudo_anterior_id",
                schema: "smsmarica",
                table: "laudo",
                column: "laudo_anterior_id");

            migrationBuilder.CreateIndex(
                name: "IX_laudo_laudo_template_id",
                schema: "smsmarica",
                table: "laudo",
                column: "laudo_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_laudo_medico_id",
                schema: "smsmarica",
                table: "laudo",
                column: "medico_id");

            migrationBuilder.CreateIndex(
                name: "IX_laudo_paciente_id",
                schema: "smsmarica",
                table: "laudo",
                column: "paciente_id");

            migrationBuilder.CreateIndex(
                name: "IX_laudo_status",
                schema: "smsmarica",
                table: "laudo",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_laudo_study_instance_uid",
                schema: "smsmarica",
                table: "laudo",
                column: "study_instance_uid");

            migrationBuilder.CreateIndex(
                name: "IX_laudo_study_instance_uid_versao",
                schema: "smsmarica",
                table: "laudo",
                columns: new[] { "study_instance_uid", "versao" },
                unique: true,
                filter: "excluido = false");

            migrationBuilder.CreateIndex(
                name: "IX_laudo_template_ativo",
                schema: "smsmarica",
                table: "laudo_template",
                column: "ativo");

            migrationBuilder.CreateIndex(
                name: "IX_laudo_template_atualizado_por_usuario_id",
                schema: "smsmarica",
                table: "laudo_template",
                column: "atualizado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_laudo_template_categoria",
                schema: "smsmarica",
                table: "laudo_template",
                column: "categoria");

            migrationBuilder.CreateIndex(
                name: "IX_laudo_template_criado_por_usuario_id",
                schema: "smsmarica",
                table: "laudo_template",
                column: "criado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_laudo_template_nome",
                schema: "smsmarica",
                table: "laudo_template",
                column: "nome",
                unique: true,
                filter: "ativo = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "laudo",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "laudo_template",
                schema: "smsmarica");
        }
    }
}
