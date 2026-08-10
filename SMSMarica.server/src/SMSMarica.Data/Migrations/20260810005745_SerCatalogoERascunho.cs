using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class SerCatalogoERascunho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ser_catalogo_lista",
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
                    table.PrimaryKey("PK_ser_catalogo_lista", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ser_catalogo_recurso",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    valor = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    rotulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    sincronizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    campos_lidos = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ser_catalogo_recurso", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ser_solicitacao_rascunho",
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
                    id_ser_gerado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    mensagem_erro = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    criado_por_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    enviado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ser_solicitacao_rascunho", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ser_catalogo_campo",
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
                    table.PrimaryKey("PK_ser_catalogo_campo", x => x.id);
                    table.ForeignKey(
                        name: "FK_ser_catalogo_campo_ser_catalogo_recurso_recurso_id",
                        column: x => x.recurso_id,
                        principalSchema: "smsmarica",
                        principalTable: "ser_catalogo_recurso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ser_rascunho_anexo",
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
                    table.PrimaryKey("PK_ser_rascunho_anexo", x => x.id);
                    table.ForeignKey(
                        name: "FK_ser_rascunho_anexo_ser_solicitacao_rascunho_rascunho_id",
                        column: x => x.rascunho_id,
                        principalSchema: "smsmarica",
                        principalTable: "ser_solicitacao_rascunho",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_ser_catalogo_campo",
                schema: "smsmarica",
                table: "ser_catalogo_campo",
                columns: new[] { "recurso_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_ser_catalogo_lista",
                schema: "smsmarica",
                table: "ser_catalogo_lista",
                columns: new[] { "lista", "valor" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_ser_catalogo_recurso",
                schema: "smsmarica",
                table: "ser_catalogo_recurso",
                columns: new[] { "tipo", "valor" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ser_rascunho_anexo_rascunho",
                schema: "smsmarica",
                table: "ser_rascunho_anexo",
                column: "rascunho_id");

            migrationBuilder.CreateIndex(
                name: "ix_ser_rascunho_status",
                schema: "smsmarica",
                table: "ser_solicitacao_rascunho",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_ser_rascunho_id_gerado",
                schema: "smsmarica",
                table: "ser_solicitacao_rascunho",
                column: "id_ser_gerado",
                unique: true,
                filter: "id_ser_gerado IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ser_catalogo_campo",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ser_catalogo_lista",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ser_rascunho_anexo",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ser_catalogo_recurso",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ser_solicitacao_rascunho",
                schema: "smsmarica");
        }
    }
}
