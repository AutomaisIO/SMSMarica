using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class SernitCatalogoERascunho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sernit_catalogo_cid_lista",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assinatura = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    sincronizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sernit_catalogo_cid_lista", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sernit_catalogo_lista",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lista = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    valor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    rotulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    sincronizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sernit_catalogo_lista", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sernit_solicitacao_rascunho",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: true),
                    recurso_valor = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    recurso_rotulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    cns = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    paciente_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    hipotese = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    campos_json = table.Column<string>(type: "jsonb", nullable: false),
                    id_sernit_gerado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    mensagem_erro = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    criado_por_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    enviado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sernit_solicitacao_rascunho", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sernit_catalogo_cid",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lista_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    descricao = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    texto = table.Column<string>(type: "character varying(420)", maxLength: 420, nullable: false),
                    busca = table.Column<string>(type: "character varying(420)", maxLength: 420, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sernit_catalogo_cid", x => x.id);
                    table.ForeignKey(
                        name: "FK_sernit_catalogo_cid_sernit_catalogo_cid_lista_lista_id",
                        column: x => x.lista_id,
                        principalSchema: "smsmarica",
                        principalTable: "sernit_catalogo_cid_lista",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sernit_catalogo_recurso",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    valor = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    rotulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    sincronizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cid_lista_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cid_assinatura = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    campos_lidos = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sernit_catalogo_recurso", x => x.id);
                    table.ForeignKey(
                        name: "FK_sernit_catalogo_recurso_sernit_catalogo_cid_lista_cid_lista~",
                        column: x => x.cid_lista_id,
                        principalSchema: "smsmarica",
                        principalTable: "sernit_catalogo_cid_lista",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sernit_rascunho_anexo",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rascunho_id = table.Column<Guid>(type: "uuid", nullable: false),
                    midia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_arquivo = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    content_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    tamanho = table.Column<long>(type: "bigint", nullable: false),
                    enviado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sernit_rascunho_anexo", x => x.id);
                    table.ForeignKey(
                        name: "FK_sernit_rascunho_anexo_sernit_solicitacao_rascunho_rascunho_~",
                        column: x => x.rascunho_id,
                        principalSchema: "smsmarica",
                        principalTable: "sernit_solicitacao_rascunho",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sernit_catalogo_campo",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recurso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    campo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    rotulo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    obrigatorio = table.Column<bool>(type: "boolean", nullable: false),
                    opcoes_json = table.Column<string>(type: "jsonb", nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sernit_catalogo_campo", x => x.id);
                    table.ForeignKey(
                        name: "FK_sernit_catalogo_campo_sernit_catalogo_recurso_recurso_id",
                        column: x => x.recurso_id,
                        principalSchema: "smsmarica",
                        principalTable: "sernit_catalogo_recurso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_sernit_catalogo_campo",
                schema: "smsmarica",
                table: "sernit_catalogo_campo",
                columns: new[] { "recurso_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sernit_catalogo_cid_busca",
                schema: "smsmarica",
                table: "sernit_catalogo_cid",
                columns: new[] { "lista_id", "busca" });

            migrationBuilder.CreateIndex(
                name: "ux_sernit_catalogo_cid",
                schema: "smsmarica",
                table: "sernit_catalogo_cid",
                columns: new[] { "lista_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_sernit_catalogo_cid_lista",
                schema: "smsmarica",
                table: "sernit_catalogo_cid_lista",
                column: "assinatura",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_sernit_catalogo_lista",
                schema: "smsmarica",
                table: "sernit_catalogo_lista",
                columns: new[] { "lista", "valor" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sernit_catalogo_recurso_cid_lista_id",
                schema: "smsmarica",
                table: "sernit_catalogo_recurso",
                column: "cid_lista_id");

            migrationBuilder.CreateIndex(
                name: "ux_sernit_catalogo_recurso",
                schema: "smsmarica",
                table: "sernit_catalogo_recurso",
                columns: new[] { "tipo", "valor" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sernit_rascunho_anexo_rascunho",
                schema: "smsmarica",
                table: "sernit_rascunho_anexo",
                column: "rascunho_id");

            migrationBuilder.CreateIndex(
                name: "ix_sernit_rascunho_status",
                schema: "smsmarica",
                table: "sernit_solicitacao_rascunho",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_sernit_rascunho_id_gerado",
                schema: "smsmarica",
                table: "sernit_solicitacao_rascunho",
                column: "id_sernit_gerado",
                unique: true,
                filter: "id_sernit_gerado IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sernit_catalogo_campo",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "sernit_catalogo_cid",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "sernit_catalogo_lista",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "sernit_rascunho_anexo",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "sernit_catalogo_recurso",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "sernit_solicitacao_rascunho",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "sernit_catalogo_cid_lista",
                schema: "smsmarica");
        }
    }
}
