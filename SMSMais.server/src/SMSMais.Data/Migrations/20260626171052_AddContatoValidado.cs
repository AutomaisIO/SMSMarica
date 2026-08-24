using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContatoValidado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "numero_validado",
                schema: "smsmarica");

            migrationBuilder.CreateTable(
                name: "contato_validado",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    validado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    validado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contato_validado", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_contato_validado_cpf",
                schema: "smsmarica",
                table: "contato_validado",
                column: "cpf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_contato_validado_numero",
                schema: "smsmarica",
                table: "contato_validado",
                column: "numero",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contato_validado",
                schema: "smsmarica");

            migrationBuilder.CreateTable(
                name: "numero_validado",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    validado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    validado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_numero_validado", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_numero_validado_numero",
                schema: "smsmarica",
                table: "numero_validado",
                column: "numero",
                unique: true);
        }
    }
}
