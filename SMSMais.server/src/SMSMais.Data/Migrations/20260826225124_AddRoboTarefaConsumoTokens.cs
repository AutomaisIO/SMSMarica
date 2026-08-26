using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoboTarefaConsumoTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "custo_usd",
                schema: "smsmarica",
                table: "robo_tarefa",
                type: "numeric(12,6)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "tokens_entrada",
                schema: "smsmarica",
                table: "robo_tarefa",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "tokens_saida",
                schema: "smsmarica",
                table: "robo_tarefa",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "custo_usd",
                schema: "smsmarica",
                table: "robo_tarefa");

            migrationBuilder.DropColumn(
                name: "tokens_entrada",
                schema: "smsmarica",
                table: "robo_tarefa");

            migrationBuilder.DropColumn(
                name: "tokens_saida",
                schema: "smsmarica",
                table: "robo_tarefa");
        }
    }
}
