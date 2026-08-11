using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class GateCpfDownloadPublico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "liberacao",
                schema: "smsmarica",
                table: "download_token",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "liberado_em",
                schema: "smsmarica",
                table: "download_token",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tentativas_cpf",
                schema: "smsmarica",
                table: "download_token",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "liberacao",
                schema: "smsmarica",
                table: "download_token");

            migrationBuilder.DropColumn(
                name: "liberado_em",
                schema: "smsmarica",
                table: "download_token");

            migrationBuilder.DropColumn(
                name: "tentativas_cpf",
                schema: "smsmarica",
                table: "download_token");
        }
    }
}
