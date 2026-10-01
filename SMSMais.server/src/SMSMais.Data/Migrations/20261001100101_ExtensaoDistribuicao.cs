using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExtensaoDistribuicao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "extensao_ativacao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    codigo_publico = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    computador = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    versao_atualizador = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    pelo_instalador = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    autorizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    autorizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    usado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dispositivo_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extensao_ativacao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "extensao_chave_publicacao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chave_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    prefixo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    criada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criada_por = table.Column<Guid>(type: "uuid", nullable: true),
                    ultimo_uso_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revogada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revogada_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extensao_chave_publicacao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "extensao_dispositivo",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    computador = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    canal = table.Column<int>(type: "integer", nullable: false),
                    autorizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    autorizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ultimo_contato_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    versao_extensao = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    versao_atualizador = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    situacao_chrome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    revogado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revogado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extensao_dispositivo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "extensao_pacote",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    artefato = table.Column<int>(type: "integer", nullable: false),
                    versao = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tamanho = table.Column<int>(type: "integer", nullable: false),
                    conteudo = table.Column<byte[]>(type: "bytea", nullable: false),
                    notas = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    publicado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    publicado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    publicado_pela_api = table.Column<bool>(type: "boolean", nullable: false),
                    promovido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    promovido_por = table.Column<Guid>(type: "uuid", nullable: true),
                    promovido_pela_api = table.Column<bool>(type: "boolean", nullable: false),
                    retirado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    retirado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extensao_pacote", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_extensao_ativacao_expira_em",
                schema: "smsmarica",
                table: "extensao_ativacao",
                column: "expira_em");

            migrationBuilder.CreateIndex(
                name: "ux_extensao_ativacao_codigo_hash",
                schema: "smsmarica",
                table: "extensao_ativacao",
                column: "codigo_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_extensao_ativacao_codigo_publico",
                schema: "smsmarica",
                table: "extensao_ativacao",
                column: "codigo_publico",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_extensao_chave_publicacao_hash",
                schema: "smsmarica",
                table: "extensao_chave_publicacao",
                column: "chave_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_extensao_dispositivo_token_hash",
                schema: "smsmarica",
                table: "extensao_dispositivo",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_extensao_pacote_artefato_versao",
                schema: "smsmarica",
                table: "extensao_pacote",
                columns: new[] { "artefato", "versao" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "extensao_ativacao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "extensao_chave_publicacao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "extensao_dispositivo",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "extensao_pacote",
                schema: "smsmarica");
        }
    }
}
