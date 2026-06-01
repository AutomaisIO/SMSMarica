using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Fhir.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEncounterCondition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "condition",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    encounter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    version_id = table.Column<int>(type: "integer", nullable: false),
                    last_updated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    meta_source = table.Column<string>(type: "text", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    content = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_condition", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "encounter",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    classe = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    period_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version_id = table.Column<int>(type: "integer", nullable: false),
                    last_updated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    meta_source = table.Column<string>(type: "text", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    content = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_encounter", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_condition_encounter_id",
                schema: "fhir",
                table: "condition",
                column: "encounter_id",
                filter: "encounter_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_condition_meta_source",
                schema: "fhir",
                table: "condition",
                column: "meta_source");

            migrationBuilder.CreateIndex(
                name: "IX_condition_patient_id",
                schema: "fhir",
                table: "condition",
                column: "patient_id",
                filter: "patient_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_encounter_meta_source",
                schema: "fhir",
                table: "encounter",
                column: "meta_source");

            migrationBuilder.CreateIndex(
                name: "IX_encounter_patient_id",
                schema: "fhir",
                table: "encounter",
                column: "patient_id",
                filter: "patient_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_encounter_period_start",
                schema: "fhir",
                table: "encounter",
                column: "period_start");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "condition",
                schema: "fhir");

            migrationBuilder.DropTable(
                name: "encounter",
                schema: "fhir");
        }
    }
}
