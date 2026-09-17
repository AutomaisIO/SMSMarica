using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkSessao24hEVinculoContato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "revogado_em",
                schema: "smsmarica",
                table: "cidadao_login_link",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "sessao_ate_em",
                schema: "smsmarica",
                table: "cidadao_login_link",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "revogado_em",
                schema: "smsmarica",
                table: "cidadao_login_link");

            migrationBuilder.DropColumn(
                name: "sessao_ate_em",
                schema: "smsmarica",
                table: "cidadao_login_link");
        }
    }
}
