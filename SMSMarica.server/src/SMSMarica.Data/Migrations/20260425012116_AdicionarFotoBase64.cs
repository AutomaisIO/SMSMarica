using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarFotoBase64 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "foto_base64",
                schema: "smsmarica",
                table: "usuario",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "foto_base64",
                schema: "smsmarica",
                table: "paciente",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "foto_base64",
                schema: "smsmarica",
                table: "motorista",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "foto_base64",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "foto_base64",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "foto_base64",
                schema: "smsmarica",
                table: "motorista");
        }
    }
}
