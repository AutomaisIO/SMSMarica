using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class SerPonteiroRetomadaEExport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "cursor_data",
                schema: "smsmarica",
                table: "ser_varredura_execucao",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cursor_id_ser",
                schema: "smsmarica",
                table: "ser_varredura_execucao",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cursor_situacao",
                schema: "smsmarica",
                table: "ser_varredura_execucao",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "fase",
                schema: "smsmarica",
                table: "ser_varredura_execucao",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "historicos_pendentes",
                schema: "smsmarica",
                table: "ser_varredura_execucao",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "retomada_em",
                schema: "smsmarica",
                table: "ser_varredura_execucao",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "retomadas",
                schema: "smsmarica",
                table: "ser_varredura_execucao",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "unidade_executora",
                schema: "smsmarica",
                table: "ser_solicitacao",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cursor_data",
                schema: "smsmarica",
                table: "ser_varredura_execucao");

            migrationBuilder.DropColumn(
                name: "cursor_id_ser",
                schema: "smsmarica",
                table: "ser_varredura_execucao");

            migrationBuilder.DropColumn(
                name: "cursor_situacao",
                schema: "smsmarica",
                table: "ser_varredura_execucao");

            migrationBuilder.DropColumn(
                name: "fase",
                schema: "smsmarica",
                table: "ser_varredura_execucao");

            migrationBuilder.DropColumn(
                name: "historicos_pendentes",
                schema: "smsmarica",
                table: "ser_varredura_execucao");

            migrationBuilder.DropColumn(
                name: "retomada_em",
                schema: "smsmarica",
                table: "ser_varredura_execucao");

            migrationBuilder.DropColumn(
                name: "retomadas",
                schema: "smsmarica",
                table: "ser_varredura_execucao");

            migrationBuilder.DropColumn(
                name: "unidade_executora",
                schema: "smsmarica",
                table: "ser_solicitacao");
        }
    }
}
