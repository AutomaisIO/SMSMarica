using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Fhir.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_reference",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    encounter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    data = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version_id = table.Column<int>(type: "integer", nullable: false),
                    last_updated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    meta_source = table.Column<string>(type: "text", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    content = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_reference", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_reference_encounter_id",
                schema: "fhir",
                table: "document_reference",
                column: "encounter_id",
                filter: "encounter_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_document_reference_meta_source",
                schema: "fhir",
                table: "document_reference",
                column: "meta_source");

            migrationBuilder.CreateIndex(
                name: "IX_document_reference_patient_id",
                schema: "fhir",
                table: "document_reference",
                column: "patient_id",
                filter: "patient_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_reference",
                schema: "fhir");
        }
    }
}
