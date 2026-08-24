using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExameImagemEquipamentoEscolhido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "equipamento_id",
                schema: "smsmarica",
                table: "exame_imagem",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_exame_imagem_equipamento_id",
                schema: "smsmarica",
                table: "exame_imagem",
                column: "equipamento_id");

            migrationBuilder.AddForeignKey(
                name: "FK_exame_imagem_equipamento_equipamento_id",
                schema: "smsmarica",
                table: "exame_imagem",
                column: "equipamento_id",
                principalSchema: "smsmarica",
                principalTable: "equipamento",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_exame_imagem_equipamento_equipamento_id",
                schema: "smsmarica",
                table: "exame_imagem");

            migrationBuilder.DropIndex(
                name: "IX_exame_imagem_equipamento_id",
                schema: "smsmarica",
                table: "exame_imagem");

            migrationBuilder.DropColumn(
                name: "equipamento_id",
                schema: "smsmarica",
                table: "exame_imagem");
        }
    }
}
