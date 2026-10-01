using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AcervoDocumentosPaciente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "documento_paciente_id",
                schema: "smsmarica",
                table: "whatsapp_mensagem",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "midia_chave",
                schema: "smsmarica",
                table: "whatsapp_mensagem",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "midia_decidida_em",
                schema: "smsmarica",
                table: "whatsapp_mensagem",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "midia_decidida_por",
                schema: "smsmarica",
                table: "whatsapp_mensagem",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "midia_legenda",
                schema: "smsmarica",
                table: "whatsapp_mensagem",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "midia_mime_type",
                schema: "smsmarica",
                table: "whatsapp_mensagem",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "midia_nome_arquivo",
                schema: "smsmarica",
                table: "whatsapp_mensagem",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "midia_sha256",
                schema: "smsmarica",
                table: "whatsapp_mensagem",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "midia_situacao",
                schema: "smsmarica",
                table: "whatsapp_mensagem",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "midia_tamanho",
                schema: "smsmarica",
                table: "whatsapp_mensagem",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "midia_wa_id",
                schema: "smsmarica",
                table: "whatsapp_mensagem",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "descricao",
                schema: "smsmarica",
                table: "sernit_rascunho_anexo",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "titulo",
                schema: "smsmarica",
                table: "sernit_rascunho_anexo",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "descricao",
                schema: "smsmarica",
                table: "ser_rascunho_anexo",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "titulo",
                schema: "smsmarica",
                table: "ser_rascunho_anexo",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "descricao",
                schema: "smsmarica",
                table: "regulacao_exigencia_arquivo",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "titulo",
                schema: "smsmarica",
                table: "regulacao_exigencia_arquivo",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "documento_paciente",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    nome_arquivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: false),
                    hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    chave_armazenamento = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    origem = table.Column<int>(type: "integer", nullable: false),
                    origem_referencia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    aceito_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    aceito_por = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_documento_paciente", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_whatsapp_mensagem_telefone_midia",
                schema: "smsmarica",
                table: "whatsapp_mensagem",
                columns: new[] { "telefone", "midia_situacao" },
                filter: "midia_situacao IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_documento_paciente_paciente_criado",
                schema: "smsmarica",
                table: "documento_paciente",
                columns: new[] { "paciente_id", "criado_em" });

            migrationBuilder.CreateIndex(
                name: "ix_documento_paciente_paciente_hash",
                schema: "smsmarica",
                table: "documento_paciente",
                columns: new[] { "paciente_id", "hash_sha256" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "documento_paciente",
                schema: "smsmarica");

            migrationBuilder.DropIndex(
                name: "ix_whatsapp_mensagem_telefone_midia",
                schema: "smsmarica",
                table: "whatsapp_mensagem");

            migrationBuilder.DropColumn(
                name: "documento_paciente_id",
                schema: "smsmarica",
                table: "whatsapp_mensagem");

            migrationBuilder.DropColumn(
                name: "midia_chave",
                schema: "smsmarica",
                table: "whatsapp_mensagem");

            migrationBuilder.DropColumn(
                name: "midia_decidida_em",
                schema: "smsmarica",
                table: "whatsapp_mensagem");

            migrationBuilder.DropColumn(
                name: "midia_decidida_por",
                schema: "smsmarica",
                table: "whatsapp_mensagem");

            migrationBuilder.DropColumn(
                name: "midia_legenda",
                schema: "smsmarica",
                table: "whatsapp_mensagem");

            migrationBuilder.DropColumn(
                name: "midia_mime_type",
                schema: "smsmarica",
                table: "whatsapp_mensagem");

            migrationBuilder.DropColumn(
                name: "midia_nome_arquivo",
                schema: "smsmarica",
                table: "whatsapp_mensagem");

            migrationBuilder.DropColumn(
                name: "midia_sha256",
                schema: "smsmarica",
                table: "whatsapp_mensagem");

            migrationBuilder.DropColumn(
                name: "midia_situacao",
                schema: "smsmarica",
                table: "whatsapp_mensagem");

            migrationBuilder.DropColumn(
                name: "midia_tamanho",
                schema: "smsmarica",
                table: "whatsapp_mensagem");

            migrationBuilder.DropColumn(
                name: "midia_wa_id",
                schema: "smsmarica",
                table: "whatsapp_mensagem");

            migrationBuilder.DropColumn(
                name: "descricao",
                schema: "smsmarica",
                table: "sernit_rascunho_anexo");

            migrationBuilder.DropColumn(
                name: "titulo",
                schema: "smsmarica",
                table: "sernit_rascunho_anexo");

            migrationBuilder.DropColumn(
                name: "descricao",
                schema: "smsmarica",
                table: "ser_rascunho_anexo");

            migrationBuilder.DropColumn(
                name: "titulo",
                schema: "smsmarica",
                table: "ser_rascunho_anexo");

            migrationBuilder.DropColumn(
                name: "descricao",
                schema: "smsmarica",
                table: "regulacao_exigencia_arquivo");

            migrationBuilder.DropColumn(
                name: "titulo",
                schema: "smsmarica",
                table: "regulacao_exigencia_arquivo");
        }
    }
}
