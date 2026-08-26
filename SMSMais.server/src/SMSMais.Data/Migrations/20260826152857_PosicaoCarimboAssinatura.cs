using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class PosicaoCarimboAssinatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "carimbo_altura",
                schema: "smsmarica",
                table: "laudo_assinatura",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "carimbo_largura",
                schema: "smsmarica",
                table: "laudo_assinatura",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "carimbo_pagina",
                schema: "smsmarica",
                table: "laudo_assinatura",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "carimbo_x",
                schema: "smsmarica",
                table: "laudo_assinatura",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "carimbo_y",
                schema: "smsmarica",
                table: "laudo_assinatura",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "pdf_base_fixado",
                schema: "smsmarica",
                table: "laudo_assinatura",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "pdf_base_hash",
                schema: "smsmarica",
                table: "laudo_assinatura",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "carimbo_altura",
                schema: "smsmarica",
                table: "laudo_assinatura");

            migrationBuilder.DropColumn(
                name: "carimbo_largura",
                schema: "smsmarica",
                table: "laudo_assinatura");

            migrationBuilder.DropColumn(
                name: "carimbo_pagina",
                schema: "smsmarica",
                table: "laudo_assinatura");

            migrationBuilder.DropColumn(
                name: "carimbo_x",
                schema: "smsmarica",
                table: "laudo_assinatura");

            migrationBuilder.DropColumn(
                name: "carimbo_y",
                schema: "smsmarica",
                table: "laudo_assinatura");

            migrationBuilder.DropColumn(
                name: "pdf_base_fixado",
                schema: "smsmarica",
                table: "laudo_assinatura");

            migrationBuilder.DropColumn(
                name: "pdf_base_hash",
                schema: "smsmarica",
                table: "laudo_assinatura");
        }
    }
}
