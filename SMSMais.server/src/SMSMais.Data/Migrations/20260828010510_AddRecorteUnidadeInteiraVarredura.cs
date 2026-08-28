using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecorteUnidadeInteiraVarredura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "recorte_unidade_inteira",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "recorte_unidade_inteira",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda");
        }
    }
}
