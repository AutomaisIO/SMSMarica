using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class SerCatalogoCid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cid_assinatura",
                schema: "smsmarica",
                table: "ser_catalogo_recurso",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cid_lista_id",
                schema: "smsmarica",
                table: "ser_catalogo_recurso",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ser_catalogo_cid_lista",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assinatura = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    sincronizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ser_catalogo_cid_lista", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ser_catalogo_cid",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lista_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    descricao = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    texto = table.Column<string>(type: "character varying(420)", maxLength: 420, nullable: false),
                    busca = table.Column<string>(type: "character varying(420)", maxLength: 420, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ser_catalogo_cid", x => x.id);
                    table.ForeignKey(
                        name: "FK_ser_catalogo_cid_ser_catalogo_cid_lista_lista_id",
                        column: x => x.lista_id,
                        principalSchema: "smsmarica",
                        principalTable: "ser_catalogo_cid_lista",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ser_catalogo_recurso_cid_lista_id",
                schema: "smsmarica",
                table: "ser_catalogo_recurso",
                column: "cid_lista_id");

            migrationBuilder.CreateIndex(
                name: "ix_ser_catalogo_cid_busca",
                schema: "smsmarica",
                table: "ser_catalogo_cid",
                columns: new[] { "lista_id", "busca" });

            migrationBuilder.CreateIndex(
                name: "ux_ser_catalogo_cid",
                schema: "smsmarica",
                table: "ser_catalogo_cid",
                columns: new[] { "lista_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_ser_catalogo_cid_lista",
                schema: "smsmarica",
                table: "ser_catalogo_cid_lista",
                column: "assinatura",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ser_catalogo_recurso_ser_catalogo_cid_lista_cid_lista_id",
                schema: "smsmarica",
                table: "ser_catalogo_recurso",
                column: "cid_lista_id",
                principalSchema: "smsmarica",
                principalTable: "ser_catalogo_cid_lista",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ser_catalogo_recurso_ser_catalogo_cid_lista_cid_lista_id",
                schema: "smsmarica",
                table: "ser_catalogo_recurso");

            migrationBuilder.DropTable(
                name: "ser_catalogo_cid",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ser_catalogo_cid_lista",
                schema: "smsmarica");

            migrationBuilder.DropIndex(
                name: "IX_ser_catalogo_recurso_cid_lista_id",
                schema: "smsmarica",
                table: "ser_catalogo_recurso");

            migrationBuilder.DropColumn(
                name: "cid_assinatura",
                schema: "smsmarica",
                table: "ser_catalogo_recurso");

            migrationBuilder.DropColumn(
                name: "cid_lista_id",
                schema: "smsmarica",
                table: "ser_catalogo_recurso");
        }
    }
}
