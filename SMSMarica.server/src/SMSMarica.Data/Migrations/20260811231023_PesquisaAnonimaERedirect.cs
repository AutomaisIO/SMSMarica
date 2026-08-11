using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class PesquisaAnonimaERedirect : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "instrumento_versao",
                schema: "smsmarica",
                table: "pesquisa_satisfacao");

            migrationBuilder.DropColumn(
                name: "respondida_ip",
                schema: "smsmarica",
                table: "pesquisa_satisfacao");

            migrationBuilder.DropColumn(
                name: "respostas",
                schema: "smsmarica",
                table: "pesquisa_satisfacao");

            migrationBuilder.RenameColumn(
                name: "respondida_em",
                schema: "smsmarica",
                table: "pesquisa_satisfacao",
                newName: "clicada_em");

            migrationBuilder.RenameIndex(
                name: "IX_pesquisa_satisfacao_respondida_em",
                schema: "smsmarica",
                table: "pesquisa_satisfacao",
                newName: "IX_pesquisa_satisfacao_clicada_em");

            migrationBuilder.AddColumn<int>(
                name: "cliques",
                schema: "smsmarica",
                table: "pesquisa_satisfacao",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "unidade_cnes",
                schema: "smsmarica",
                table: "pesquisa_satisfacao",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "wa_message_id",
                schema: "smsmarica",
                table: "pesquisa_satisfacao",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "unidade_pesquisa_config",
                schema: "smsmarica",
                columns: table => new
                {
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    envio_whatsapp_ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    link_responder = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    link_painel = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    horas_apos_atendimento = table.Column<int>(type: "integer", nullable: false, defaultValue: 24),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unidade_pesquisa_config", x => x.unidade_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "unidade_pesquisa_config",
                schema: "smsmarica");

            migrationBuilder.DropColumn(
                name: "cliques",
                schema: "smsmarica",
                table: "pesquisa_satisfacao");

            migrationBuilder.DropColumn(
                name: "unidade_cnes",
                schema: "smsmarica",
                table: "pesquisa_satisfacao");

            migrationBuilder.DropColumn(
                name: "wa_message_id",
                schema: "smsmarica",
                table: "pesquisa_satisfacao");

            migrationBuilder.RenameColumn(
                name: "clicada_em",
                schema: "smsmarica",
                table: "pesquisa_satisfacao",
                newName: "respondida_em");

            migrationBuilder.RenameIndex(
                name: "IX_pesquisa_satisfacao_clicada_em",
                schema: "smsmarica",
                table: "pesquisa_satisfacao",
                newName: "IX_pesquisa_satisfacao_respondida_em");

            migrationBuilder.AddColumn<string>(
                name: "instrumento_versao",
                schema: "smsmarica",
                table: "pesquisa_satisfacao",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "respondida_ip",
                schema: "smsmarica",
                table: "pesquisa_satisfacao",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "respostas",
                schema: "smsmarica",
                table: "pesquisa_satisfacao",
                type: "jsonb",
                nullable: true);
        }
    }
}
