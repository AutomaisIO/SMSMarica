using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCidadaoLoginLinkEMagicLinkValidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "magic_link_validade_dias",
                schema: "smsmarica",
                table: "laudo_configuracao",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.CreateTable(
                name: "cidadao_login_link",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    destino = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usado_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cidadao_login_link", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cidadao_login_link_patient_id",
                schema: "smsmarica",
                table: "cidadao_login_link",
                column: "patient_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cidadao_login_link",
                schema: "smsmarica");

            migrationBuilder.DropColumn(
                name: "magic_link_validade_dias",
                schema: "smsmarica",
                table: "laudo_configuracao");
        }
    }
}
