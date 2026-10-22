using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class TransladoResgate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_translado_alocacao_rota_diaria_id_assento_id",
                schema: "smsmarica",
                table: "translado_alocacao");

            migrationBuilder.RenameColumn(
                name: "volta",
                schema: "smsmarica",
                table: "translado_evento_viagem",
                newName: "resgate");

            migrationBuilder.AddColumn<DateTime>(
                name: "ainda_em_atendimento_em",
                schema: "smsmarica",
                table: "sessao_de_tratamento",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "pergunta_resgate_enviada_em",
                schema: "smsmarica",
                table: "sessao_de_tratamento",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "pronto_resgate_canal",
                schema: "smsmarica",
                table: "sessao_de_tratamento",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "pronto_resgate_em",
                schema: "smsmarica",
                table: "sessao_de_tratamento",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_translado_alocacao_rota_diaria_id_assento_id",
                schema: "smsmarica",
                table: "translado_alocacao",
                columns: new[] { "rota_diaria_id", "assento_id" },
                unique: true,
                filter: "tipo_parada <> 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_translado_alocacao_rota_diaria_id_assento_id",
                schema: "smsmarica",
                table: "translado_alocacao");

            migrationBuilder.DropColumn(
                name: "ainda_em_atendimento_em",
                schema: "smsmarica",
                table: "sessao_de_tratamento");

            migrationBuilder.DropColumn(
                name: "pergunta_resgate_enviada_em",
                schema: "smsmarica",
                table: "sessao_de_tratamento");

            migrationBuilder.DropColumn(
                name: "pronto_resgate_canal",
                schema: "smsmarica",
                table: "sessao_de_tratamento");

            migrationBuilder.DropColumn(
                name: "pronto_resgate_em",
                schema: "smsmarica",
                table: "sessao_de_tratamento");

            migrationBuilder.RenameColumn(
                name: "resgate",
                schema: "smsmarica",
                table: "translado_evento_viagem",
                newName: "volta");

            migrationBuilder.CreateIndex(
                name: "IX_translado_alocacao_rota_diaria_id_assento_id",
                schema: "smsmarica",
                table: "translado_alocacao",
                columns: new[] { "rota_diaria_id", "assento_id" },
                unique: true);
        }
    }
}
