using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class DiasSemanaAtendimentoHumano : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "dias_semana_atendimento_humano",
                schema: "smsmarica",
                table: "robo_configuracao",
                type: "integer",
                nullable: true,
                defaultValue: 62);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dias_semana_atendimento_humano",
                schema: "smsmarica",
                table: "robo_configuracao");
        }
    }
}
