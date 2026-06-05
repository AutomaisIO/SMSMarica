using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Fhir.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddObservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "observation",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    encounter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    effective = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version_id = table.Column<int>(type: "integer", nullable: false),
                    last_updated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    meta_source = table.Column<string>(type: "text", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    content = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_observation", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_observation_code",
                schema: "fhir",
                table: "observation",
                column: "code",
                filter: "code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_observation_encounter_id",
                schema: "fhir",
                table: "observation",
                column: "encounter_id",
                filter: "encounter_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_observation_meta_source",
                schema: "fhir",
                table: "observation",
                column: "meta_source");

            migrationBuilder.CreateIndex(
                name: "IX_observation_patient_id",
                schema: "fhir",
                table: "observation",
                column: "patient_id",
                filter: "patient_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "observation",
                schema: "fhir");
        }
    }
}
