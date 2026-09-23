using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class SiscanRequisicaoNoExameImagem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "siscan_erro",
                schema: "smsmarica",
                table: "exame_imagem",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "siscan_numero_exame",
                schema: "smsmarica",
                table: "exame_imagem",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "siscan_protocolo",
                schema: "smsmarica",
                table: "exame_imagem",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "siscan_requisicao_em",
                schema: "smsmarica",
                table: "exame_imagem",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "siscan_requisicao_por",
                schema: "smsmarica",
                table: "exame_imagem",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_exame_imagem_siscan_protocolo",
                schema: "smsmarica",
                table: "exame_imagem",
                column: "siscan_protocolo",
                filter: "siscan_protocolo IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_exame_imagem_siscan_protocolo",
                schema: "smsmarica",
                table: "exame_imagem");

            migrationBuilder.DropColumn(
                name: "siscan_erro",
                schema: "smsmarica",
                table: "exame_imagem");

            migrationBuilder.DropColumn(
                name: "siscan_numero_exame",
                schema: "smsmarica",
                table: "exame_imagem");

            migrationBuilder.DropColumn(
                name: "siscan_protocolo",
                schema: "smsmarica",
                table: "exame_imagem");

            migrationBuilder.DropColumn(
                name: "siscan_requisicao_em",
                schema: "smsmarica",
                table: "exame_imagem");

            migrationBuilder.DropColumn(
                name: "siscan_requisicao_por",
                schema: "smsmarica",
                table: "exame_imagem");
        }
    }
}
