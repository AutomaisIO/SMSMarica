using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class SisregEnderecoIp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sisreg_endereco_ip",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    desde_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ultima_vez_visto_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ate_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    interface_rota = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sisreg_endereco_ip", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_sisreg_endereco_ip_aberto",
                schema: "smsmarica",
                table: "sisreg_endereco_ip",
                column: "ip",
                unique: true,
                filter: "ate_em IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sisreg_endereco_ip",
                schema: "smsmarica");
        }
    }
}
