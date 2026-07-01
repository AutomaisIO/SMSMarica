using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaUnidadeSolicitanteNaSolicitacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "unidade_solicitante_id",
                schema: "smsmarica",
                table: "solicitacao_exame",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_exame_unidade_solicitante_id",
                schema: "smsmarica",
                table: "solicitacao_exame",
                column: "unidade_solicitante_id");

            migrationBuilder.AddForeignKey(
                name: "FK_solicitacao_exame_unidade_unidade_solicitante_id",
                schema: "smsmarica",
                table: "solicitacao_exame",
                column: "unidade_solicitante_id",
                principalSchema: "smsmarica",
                principalTable: "unidade",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_solicitacao_exame_unidade_unidade_solicitante_id",
                schema: "smsmarica",
                table: "solicitacao_exame");

            migrationBuilder.DropIndex(
                name: "IX_solicitacao_exame_unidade_solicitante_id",
                schema: "smsmarica",
                table: "solicitacao_exame");

            migrationBuilder.DropColumn(
                name: "unidade_solicitante_id",
                schema: "smsmarica",
                table: "solicitacao_exame");
        }
    }
}
