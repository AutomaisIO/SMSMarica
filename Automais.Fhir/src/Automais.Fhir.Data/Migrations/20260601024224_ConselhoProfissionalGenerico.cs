using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Fhir.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConselhoProfissionalGenerico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_practitioner_crm",
                schema: "fhir",
                table: "practitioner");

            migrationBuilder.RenameColumn(
                name: "crm",
                schema: "fhir",
                table: "practitioner",
                newName: "registro");

            migrationBuilder.AddColumn<string>(
                name: "conselho",
                schema: "fhir",
                table: "practitioner",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_practitioner_conselho",
                schema: "fhir",
                table: "practitioner",
                column: "conselho",
                filter: "conselho IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_practitioner_registro",
                schema: "fhir",
                table: "practitioner",
                column: "registro",
                filter: "registro IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_practitioner_conselho",
                schema: "fhir",
                table: "practitioner");

            migrationBuilder.DropIndex(
                name: "IX_practitioner_registro",
                schema: "fhir",
                table: "practitioner");

            migrationBuilder.DropColumn(
                name: "conselho",
                schema: "fhir",
                table: "practitioner");

            migrationBuilder.RenameColumn(
                name: "registro",
                schema: "fhir",
                table: "practitioner",
                newName: "crm");

            migrationBuilder.CreateIndex(
                name: "IX_practitioner_crm",
                schema: "fhir",
                table: "practitioner",
                column: "crm",
                filter: "crm IS NOT NULL");
        }
    }
}
