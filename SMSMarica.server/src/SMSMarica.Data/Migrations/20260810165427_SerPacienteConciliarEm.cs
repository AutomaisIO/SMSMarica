using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class SerPacienteConciliarEm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "paciente_conciliar_em",
                schema: "smsmarica",
                table: "ser_solicitacao",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_ser_solicitacao_paciente_conciliar",
                schema: "smsmarica",
                table: "ser_solicitacao",
                column: "paciente_conciliar_em",
                filter: "paciente_conciliar_em IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ser_solicitacao_paciente_conciliar",
                schema: "smsmarica",
                table: "ser_solicitacao");

            migrationBuilder.DropColumn(
                name: "paciente_conciliar_em",
                schema: "smsmarica",
                table: "ser_solicitacao");
        }
    }
}
