using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Fhir.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentifierClinicoELocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "identifier_system",
                schema: "fhir",
                table: "observation",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identifier_value",
                schema: "fhir",
                table: "observation",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identifier_system",
                schema: "fhir",
                table: "medication_request",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identifier_value",
                schema: "fhir",
                table: "medication_request",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identifier_system",
                schema: "fhir",
                table: "encounter",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identifier_value",
                schema: "fhir",
                table: "encounter",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "period_end",
                schema: "fhir",
                table: "encounter",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identifier_system",
                schema: "fhir",
                table: "document_reference",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identifier_value",
                schema: "fhir",
                table: "document_reference",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identifier_system",
                schema: "fhir",
                table: "condition",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identifier_value",
                schema: "fhir",
                table: "condition",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "location",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    physical_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    part_of_id = table.Column<Guid>(type: "uuid", nullable: true),
                    identifier_system = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    identifier_value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    version_id = table.Column<int>(type: "integer", nullable: false),
                    last_updated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    meta_source = table.Column<string>(type: "text", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    content = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_location", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_observation_identifier_system_identifier_value",
                schema: "fhir",
                table: "observation",
                columns: new[] { "identifier_system", "identifier_value" },
                unique: true,
                filter: "identifier_system IS NOT NULL AND NOT is_deleted");

            migrationBuilder.CreateIndex(
                name: "IX_medication_request_identifier_system_identifier_value",
                schema: "fhir",
                table: "medication_request",
                columns: new[] { "identifier_system", "identifier_value" },
                unique: true,
                filter: "identifier_system IS NOT NULL AND NOT is_deleted");

            migrationBuilder.CreateIndex(
                name: "IX_encounter_identifier_system_identifier_value",
                schema: "fhir",
                table: "encounter",
                columns: new[] { "identifier_system", "identifier_value" },
                unique: true,
                filter: "identifier_system IS NOT NULL AND NOT is_deleted");

            migrationBuilder.CreateIndex(
                name: "IX_encounter_period_end",
                schema: "fhir",
                table: "encounter",
                column: "period_end",
                filter: "period_end IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_document_reference_identifier_system_identifier_value",
                schema: "fhir",
                table: "document_reference",
                columns: new[] { "identifier_system", "identifier_value" },
                unique: true,
                filter: "identifier_system IS NOT NULL AND NOT is_deleted");

            migrationBuilder.CreateIndex(
                name: "IX_condition_identifier_system_identifier_value",
                schema: "fhir",
                table: "condition",
                columns: new[] { "identifier_system", "identifier_value" },
                unique: true,
                filter: "identifier_system IS NOT NULL AND NOT is_deleted");

            migrationBuilder.CreateIndex(
                name: "IX_location_identifier_system_identifier_value",
                schema: "fhir",
                table: "location",
                columns: new[] { "identifier_system", "identifier_value" },
                unique: true,
                filter: "identifier_system IS NOT NULL AND NOT is_deleted");

            migrationBuilder.CreateIndex(
                name: "IX_location_meta_source",
                schema: "fhir",
                table: "location",
                column: "meta_source");

            migrationBuilder.CreateIndex(
                name: "IX_location_part_of_id",
                schema: "fhir",
                table: "location",
                column: "part_of_id",
                filter: "part_of_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "location",
                schema: "fhir");

            migrationBuilder.DropIndex(
                name: "IX_observation_identifier_system_identifier_value",
                schema: "fhir",
                table: "observation");

            migrationBuilder.DropIndex(
                name: "IX_medication_request_identifier_system_identifier_value",
                schema: "fhir",
                table: "medication_request");

            migrationBuilder.DropIndex(
                name: "IX_encounter_identifier_system_identifier_value",
                schema: "fhir",
                table: "encounter");

            migrationBuilder.DropIndex(
                name: "IX_encounter_period_end",
                schema: "fhir",
                table: "encounter");

            migrationBuilder.DropIndex(
                name: "IX_document_reference_identifier_system_identifier_value",
                schema: "fhir",
                table: "document_reference");

            migrationBuilder.DropIndex(
                name: "IX_condition_identifier_system_identifier_value",
                schema: "fhir",
                table: "condition");

            migrationBuilder.DropColumn(
                name: "identifier_system",
                schema: "fhir",
                table: "observation");

            migrationBuilder.DropColumn(
                name: "identifier_value",
                schema: "fhir",
                table: "observation");

            migrationBuilder.DropColumn(
                name: "identifier_system",
                schema: "fhir",
                table: "medication_request");

            migrationBuilder.DropColumn(
                name: "identifier_value",
                schema: "fhir",
                table: "medication_request");

            migrationBuilder.DropColumn(
                name: "identifier_system",
                schema: "fhir",
                table: "encounter");

            migrationBuilder.DropColumn(
                name: "identifier_value",
                schema: "fhir",
                table: "encounter");

            migrationBuilder.DropColumn(
                name: "period_end",
                schema: "fhir",
                table: "encounter");

            migrationBuilder.DropColumn(
                name: "identifier_system",
                schema: "fhir",
                table: "document_reference");

            migrationBuilder.DropColumn(
                name: "identifier_value",
                schema: "fhir",
                table: "document_reference");

            migrationBuilder.DropColumn(
                name: "identifier_system",
                schema: "fhir",
                table: "condition");

            migrationBuilder.DropColumn(
                name: "identifier_value",
                schema: "fhir",
                table: "condition");
        }
    }
}
