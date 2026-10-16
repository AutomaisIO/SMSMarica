using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AvaliacaoDoTranslado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "canal",
                schema: "smsmarica",
                table: "avaliacao",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "motorista_id",
                schema: "smsmarica",
                table: "avaliacao",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "rota_id",
                schema: "smsmarica",
                table: "avaliacao",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "veiculo_id",
                schema: "smsmarica",
                table: "avaliacao",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_avaliacao_motorista_id",
                schema: "smsmarica",
                table: "avaliacao",
                column: "motorista_id");

            migrationBuilder.CreateIndex(
                name: "IX_avaliacao_rota_id",
                schema: "smsmarica",
                table: "avaliacao",
                column: "rota_id");

            migrationBuilder.CreateIndex(
                name: "IX_avaliacao_veiculo_id",
                schema: "smsmarica",
                table: "avaliacao",
                column: "veiculo_id");

            migrationBuilder.AddForeignKey(
                name: "FK_avaliacao_motorista_motorista_id",
                schema: "smsmarica",
                table: "avaliacao",
                column: "motorista_id",
                principalSchema: "smsmarica",
                principalTable: "motorista",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_avaliacao_translado_rota_diaria_rota_id",
                schema: "smsmarica",
                table: "avaliacao",
                column: "rota_id",
                principalSchema: "smsmarica",
                principalTable: "translado_rota_diaria",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_avaliacao_veiculo_veiculo_id",
                schema: "smsmarica",
                table: "avaliacao",
                column: "veiculo_id",
                principalSchema: "smsmarica",
                principalTable: "veiculo",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_avaliacao_motorista_motorista_id",
                schema: "smsmarica",
                table: "avaliacao");

            migrationBuilder.DropForeignKey(
                name: "FK_avaliacao_translado_rota_diaria_rota_id",
                schema: "smsmarica",
                table: "avaliacao");

            migrationBuilder.DropForeignKey(
                name: "FK_avaliacao_veiculo_veiculo_id",
                schema: "smsmarica",
                table: "avaliacao");

            migrationBuilder.DropIndex(
                name: "IX_avaliacao_motorista_id",
                schema: "smsmarica",
                table: "avaliacao");

            migrationBuilder.DropIndex(
                name: "IX_avaliacao_rota_id",
                schema: "smsmarica",
                table: "avaliacao");

            migrationBuilder.DropIndex(
                name: "IX_avaliacao_veiculo_id",
                schema: "smsmarica",
                table: "avaliacao");

            migrationBuilder.DropColumn(
                name: "canal",
                schema: "smsmarica",
                table: "avaliacao");

            migrationBuilder.DropColumn(
                name: "motorista_id",
                schema: "smsmarica",
                table: "avaliacao");

            migrationBuilder.DropColumn(
                name: "rota_id",
                schema: "smsmarica",
                table: "avaliacao");

            migrationBuilder.DropColumn(
                name: "veiculo_id",
                schema: "smsmarica",
                table: "avaliacao");
        }
    }
}
