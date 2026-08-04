using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Fhir.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organization",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    cnes = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ativa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_organization", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_organization_cnes",
                schema: "fhir",
                table: "organization",
                column: "cnes",
                filter: "cnes IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_organization_identifier_system_identifier_value",
                schema: "fhir",
                table: "organization",
                columns: new[] { "identifier_system", "identifier_value" },
                unique: true,
                filter: "identifier_system IS NOT NULL AND NOT is_deleted");

            migrationBuilder.CreateIndex(
                name: "IX_organization_meta_source",
                schema: "fhir",
                table: "organization",
                column: "meta_source");

            migrationBuilder.CreateIndex(
                name: "IX_organization_part_of_id",
                schema: "fhir",
                table: "organization",
                column: "part_of_id",
                filter: "part_of_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organization",
                schema: "fhir");
        }
    }
}
