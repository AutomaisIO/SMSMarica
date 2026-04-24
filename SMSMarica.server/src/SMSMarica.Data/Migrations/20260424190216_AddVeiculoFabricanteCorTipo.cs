using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVeiculoFabricanteCorTipo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cor",
                schema: "smsmarica",
                table: "veiculo",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "fabricante",
                schema: "smsmarica",
                table: "veiculo",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "tipo",
                schema: "smsmarica",
                table: "veiculo",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cor",
                schema: "smsmarica",
                table: "veiculo");

            migrationBuilder.DropColumn(
                name: "fabricante",
                schema: "smsmarica",
                table: "veiculo");

            migrationBuilder.DropColumn(
                name: "tipo",
                schema: "smsmarica",
                table: "veiculo");
        }
    }
}
