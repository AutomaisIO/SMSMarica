using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AvaliacaoPorTrecho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_avaliacao_sessao_id",
                schema: "smsmarica",
                table: "avaliacao");

            migrationBuilder.AddColumn<int>(
                name: "trecho",
                schema: "smsmarica",
                table: "avaliacao",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_avaliacao_sessao_id_trecho",
                schema: "smsmarica",
                table: "avaliacao",
                columns: new[] { "sessao_id", "trecho" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_avaliacao_sessao_id_trecho",
                schema: "smsmarica",
                table: "avaliacao");

            migrationBuilder.DropColumn(
                name: "trecho",
                schema: "smsmarica",
                table: "avaliacao");

            migrationBuilder.CreateIndex(
                name: "IX_avaliacao_sessao_id",
                schema: "smsmarica",
                table: "avaliacao",
                column: "sessao_id",
                unique: true);
        }
    }
}
