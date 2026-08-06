using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContadorPessoasInalteradas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "medicos_inalterados",
                schema: "smsmarica",
                table: "pep_sincronizacao_execucao",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "pacientes_inalterados",
                schema: "smsmarica",
                table: "pep_sincronizacao_execucao",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "medicos_inalterados",
                schema: "smsmarica",
                table: "pep_sincronizacao_execucao");

            migrationBuilder.DropColumn(
                name: "pacientes_inalterados",
                schema: "smsmarica",
                table: "pep_sincronizacao_execucao");
        }
    }
}
