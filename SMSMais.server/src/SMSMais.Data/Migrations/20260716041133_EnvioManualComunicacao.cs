using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnvioManualComunicacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "enviado_por",
                schema: "smsmarica",
                table: "comunicacao_paciente",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ignorar_verificacao_telefone",
                schema: "smsmarica",
                table: "comunicacao_paciente",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "origem",
                schema: "smsmarica",
                table: "comunicacao_paciente",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "enviado_por",
                schema: "smsmarica",
                table: "comunicacao_paciente");

            migrationBuilder.DropColumn(
                name: "ignorar_verificacao_telefone",
                schema: "smsmarica",
                table: "comunicacao_paciente");

            migrationBuilder.DropColumn(
                name: "origem",
                schema: "smsmarica",
                table: "comunicacao_paciente");
        }
    }
}
