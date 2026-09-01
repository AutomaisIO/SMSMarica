using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveVarreduraPorCombinacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A varredura por par profissional × procedimento morreu: a agenda da unidade vem
            // inteira numa requisição, e de quebra atualiza o mapeamento. O que sai aqui é a
            // configuração e o cursor daquele modo.
            //
            // PERDA CONSCIENTE: 53 execuções guardavam em qual par pararam. É rastro de um
            // mecanismo que não existe mais — ninguém vai retomar aquelas varreduras. As contagens
            // de cobertura (combinacoes_total/feitas) FICAM: essas contam o que de fato aconteceu.

            migrationBuilder.DropColumn(
                name: "cursor_procedimento_codigo",
                schema: "smsmarica",
                table: "sisreg_varredura_execucao");

            migrationBuilder.DropColumn(
                name: "cursor_profissional_cpf",
                schema: "smsmarica",
                table: "sisreg_varredura_execucao");

            migrationBuilder.DropColumn(
                name: "cursor_janela_fim",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda");

            migrationBuilder.DropColumn(
                name: "cursor_procedimento_codigo",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda");

            migrationBuilder.DropColumn(
                name: "cursor_profissional_cpf",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda");

            migrationBuilder.DropColumn(
                name: "recorte_unidade_inteira",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cursor_procedimento_codigo",
                schema: "smsmarica",
                table: "sisreg_varredura_execucao",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cursor_profissional_cpf",
                schema: "smsmarica",
                table: "sisreg_varredura_execucao",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "cursor_janela_fim",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cursor_procedimento_codigo",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cursor_profissional_cpf",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "recorte_unidade_inteira",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }
    }
}
