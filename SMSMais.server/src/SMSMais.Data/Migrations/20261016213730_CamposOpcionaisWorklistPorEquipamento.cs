using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class CamposOpcionaisWorklistPorEquipamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "codigo_procedimento_tag",
                schema: "smsmarica",
                table: "equipamento",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "medico_solicitante_tag",
                schema: "smsmarica",
                table: "equipamento",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "unidade_origem",
                schema: "smsmarica",
                table: "equipamento",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "unidade_tag",
                schema: "smsmarica",
                table: "equipamento",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "codigo_procedimento_tag",
                schema: "smsmarica",
                table: "equipamento");

            migrationBuilder.DropColumn(
                name: "medico_solicitante_tag",
                schema: "smsmarica",
                table: "equipamento");

            migrationBuilder.DropColumn(
                name: "unidade_origem",
                schema: "smsmarica",
                table: "equipamento");

            migrationBuilder.DropColumn(
                name: "unidade_tag",
                schema: "smsmarica",
                table: "equipamento");
        }
    }
}
