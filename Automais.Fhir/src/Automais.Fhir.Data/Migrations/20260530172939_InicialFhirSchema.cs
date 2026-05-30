using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Fhir.Data.Migrations
{
    /// <inheritdoc />
    public partial class InicialFhirSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "fhir");

            migrationBuilder.CreateTable(
                name: "patient",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    cns = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    nascimento = table.Column<DateOnly>(type: "date", nullable: true),
                    version_id = table.Column<int>(type: "integer", nullable: false),
                    last_updated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    meta_source = table.Column<string>(type: "text", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    content = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_patient_cns",
                schema: "fhir",
                table: "patient",
                column: "cns",
                filter: "cns IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_patient_cpf",
                schema: "fhir",
                table: "patient",
                column: "cpf",
                filter: "cpf IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_patient_meta_source",
                schema: "fhir",
                table: "patient",
                column: "meta_source");

            migrationBuilder.CreateIndex(
                name: "IX_patient_nome",
                schema: "fhir",
                table: "patient",
                column: "nome");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "patient",
                schema: "fhir");
        }
    }
}
