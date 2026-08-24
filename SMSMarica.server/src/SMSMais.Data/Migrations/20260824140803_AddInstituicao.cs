using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInstituicao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "instituicao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nome_secretaria = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nome_curto = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    sigla = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    codigo_ibge = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ddd_padrao = table.Column<int>(type: "integer", nullable: true),
                    endereco_cep = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    endereco_logradouro = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    endereco_numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    endereco_complemento = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    endereco_bairro = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    endereco_cidade = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    endereco_uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    endereco_ponto_referencia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    telefone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email_contato = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    email_dpo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    whatsapp_numero_publico = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    logo_midia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    favicon_midia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cor_primaria = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    cor_secundaria = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    cor_gradiente_inicio = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    cor_gradiente_fim = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    url_painel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    url_app = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    url_arquivos = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    assinatura_produto_html = table.Column<string>(type: "text", nullable: true),
                    atualizado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_instituicao", x => x.id);
                    table.ForeignKey(
                        name: "FK_instituicao_midia_favicon_midia_id",
                        column: x => x.favicon_midia_id,
                        principalSchema: "smsmarica",
                        principalTable: "midia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_instituicao_midia_logo_midia_id",
                        column: x => x.logo_midia_id,
                        principalSchema: "smsmarica",
                        principalTable: "midia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_instituicao_usuario_atualizado_por_usuario_id",
                        column: x => x.atualizado_por_usuario_id,
                        principalSchema: "smsmarica",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_instituicao_atualizado_por_usuario_id",
                schema: "smsmarica",
                table: "instituicao",
                column: "atualizado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_instituicao_favicon_midia_id",
                schema: "smsmarica",
                table: "instituicao",
                column: "favicon_midia_id");

            migrationBuilder.CreateIndex(
                name: "IX_instituicao_logo_midia_id",
                schema: "smsmarica",
                table: "instituicao",
                column: "logo_midia_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "instituicao",
                schema: "smsmarica");
        }
    }
}
