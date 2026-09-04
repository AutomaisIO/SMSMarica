using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProfissionalExecutanteSolicitacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "profissional_executante_cpf",
                schema: "smsmarica",
                table: "solicitacao",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "profissional_executante_nome",
                schema: "smsmarica",
                table: "solicitacao",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_profissional_executante_cpf_data_agendada",
                schema: "smsmarica",
                table: "solicitacao",
                columns: new[] { "profissional_executante_cpf", "data_agendada" },
                filter: "profissional_executante_cpf IS NOT NULL AND data_agendada IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_solicitacao_profissional_executante_cpf_data_agendada",
                schema: "smsmarica",
                table: "solicitacao");

            migrationBuilder.DropColumn(
                name: "profissional_executante_cpf",
                schema: "smsmarica",
                table: "solicitacao");

            migrationBuilder.DropColumn(
                name: "profissional_executante_nome",
                schema: "smsmarica",
                table: "solicitacao");
        }
    }
}
