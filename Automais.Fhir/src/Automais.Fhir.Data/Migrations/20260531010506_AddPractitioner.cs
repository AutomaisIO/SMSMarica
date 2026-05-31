using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Fhir.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPractitioner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "practitioner",
                schema: "fhir",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    crm = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    version_id = table.Column<int>(type: "integer", nullable: false),
                    last_updated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    meta_source = table.Column<string>(type: "text", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    content = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_practitioner", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_practitioner_cpf",
                schema: "fhir",
                table: "practitioner",
                column: "cpf",
                filter: "cpf IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_practitioner_crm",
                schema: "fhir",
                table: "practitioner",
                column: "crm",
                filter: "crm IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_practitioner_meta_source",
                schema: "fhir",
                table: "practitioner",
                column: "meta_source");

            migrationBuilder.CreateIndex(
                name: "IX_practitioner_nome",
                schema: "fhir",
                table: "practitioner",
                column: "nome");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "practitioner",
                schema: "fhir");
        }
    }
}
