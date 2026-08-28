using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFonteCadastroPacienteSisreg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "fonte_cadastro_paciente",
                schema: "smsmarica",
                table: "sisreg_configuracao",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fonte_cadastro_paciente",
                schema: "smsmarica",
                table: "sisreg_configuracao");
        }
    }
}
