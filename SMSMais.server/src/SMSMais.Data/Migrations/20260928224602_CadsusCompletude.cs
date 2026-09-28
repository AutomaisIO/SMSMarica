using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class CadsusCompletude : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cadsus_completude",
                schema: "smsmarica",
                columns: table => new
                {
                    cns = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    consultado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    desfecho = table.Column<int>(type: "integer", nullable: false),
                    paciente_destino_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cadsus_completude", x => x.cns);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cadsus_completude",
                schema: "smsmarica");
        }
    }
}
