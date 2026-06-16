using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Fhir.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTelefoneSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "telefone",
                schema: "fhir",
                table: "patient",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_patient_telefone",
                schema: "fhir",
                table: "patient",
                column: "telefone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_patient_telefone",
                schema: "fhir",
                table: "patient");

            migrationBuilder.DropColumn(
                name: "telefone",
                schema: "fhir",
                table: "patient");
        }
    }
}
