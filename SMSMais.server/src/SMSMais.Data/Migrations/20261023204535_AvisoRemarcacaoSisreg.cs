using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AvisoRemarcacaoSisreg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "aviso_automatico_em",
                schema: "smsmarica",
                table: "sisreg_alteracao_agenda",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nota_aviso",
                schema: "smsmarica",
                table: "sisreg_alteracao_agenda",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "aviso_remarcacao_habilitado",
                schema: "smsmarica",
                table: "confirmacao_configuracao",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "aviso_remarcacao_ligado_em",
                schema: "smsmarica",
                table: "confirmacao_configuracao",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "remarcada_de",
                schema: "smsmarica",
                table: "comunicacao_paciente",
                type: "timestamp with time zone",
                nullable: true);

            // O aviso nasce ligado, e ligar vale daqui para frente: a fila de Alterações da agenda
            // guarda remarcações desde 04/09/2026, e sem este corte o automático avisaria de uma vez
            // todas as que ainda estão pendentes. Instância sem a linha (nova) não tem passado: fica
            // sem corte. Não é dado institucional — é o carimbo da virada, igual em toda instância.
            migrationBuilder.Sql(
                "UPDATE smsmarica.confirmacao_configuracao SET aviso_remarcacao_ligado_em = now() "
                + "WHERE aviso_remarcacao_habilitado;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "aviso_automatico_em",
                schema: "smsmarica",
                table: "sisreg_alteracao_agenda");

            migrationBuilder.DropColumn(
                name: "nota_aviso",
                schema: "smsmarica",
                table: "sisreg_alteracao_agenda");

            migrationBuilder.DropColumn(
                name: "aviso_remarcacao_habilitado",
                schema: "smsmarica",
                table: "confirmacao_configuracao");

            migrationBuilder.DropColumn(
                name: "aviso_remarcacao_ligado_em",
                schema: "smsmarica",
                table: "confirmacao_configuracao");

            migrationBuilder.DropColumn(
                name: "remarcada_de",
                schema: "smsmarica",
                table: "comunicacao_paciente");
        }
    }
}
