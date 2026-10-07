using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class CatalogosRegulacaoIdentidadePorNome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_sernit_catalogo_recurso",
                schema: "smsmarica",
                table: "sernit_catalogo_recurso");

            migrationBuilder.DropIndex(
                name: "ux_ser_catalogo_recurso",
                schema: "smsmarica",
                table: "ser_catalogo_recurso");

            migrationBuilder.DropIndex(
                name: "ux_esussg_catalogo_recurso",
                schema: "smsmarica",
                table: "esussg_catalogo_recurso");

            migrationBuilder.AddColumn<string>(
                name: "rotulo_chave",
                schema: "smsmarica",
                table: "sernit_catalogo_recurso",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rotulo_chave",
                schema: "smsmarica",
                table: "ser_catalogo_recurso",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rotulo_chave",
                schema: "smsmarica",
                table: "esussg_catalogo_recurso",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_sernit_catalogo_recurso_valor",
                schema: "smsmarica",
                table: "sernit_catalogo_recurso",
                columns: new[] { "tipo", "valor" });

            migrationBuilder.CreateIndex(
                name: "ux_sernit_catalogo_recurso_nome",
                schema: "smsmarica",
                table: "sernit_catalogo_recurso",
                columns: new[] { "tipo", "rotulo_chave" },
                unique: true,
                filter: "rotulo_chave IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_ser_catalogo_recurso_valor",
                schema: "smsmarica",
                table: "ser_catalogo_recurso",
                columns: new[] { "tipo", "ambulatorio_estadual", "valor" });

            migrationBuilder.CreateIndex(
                name: "ux_ser_catalogo_recurso_nome",
                schema: "smsmarica",
                table: "ser_catalogo_recurso",
                columns: new[] { "tipo", "ambulatorio_estadual", "rotulo_chave" },
                unique: true,
                filter: "rotulo_chave IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_esussg_catalogo_recurso_valor",
                schema: "smsmarica",
                table: "esussg_catalogo_recurso",
                columns: new[] { "tipo", "valor" });

            migrationBuilder.CreateIndex(
                name: "ux_esussg_catalogo_recurso_nome",
                schema: "smsmarica",
                table: "esussg_catalogo_recurso",
                columns: new[] { "tipo", "rotulo_chave" },
                unique: true,
                filter: "rotulo_chave IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sernit_catalogo_recurso_valor",
                schema: "smsmarica",
                table: "sernit_catalogo_recurso");

            migrationBuilder.DropIndex(
                name: "ux_sernit_catalogo_recurso_nome",
                schema: "smsmarica",
                table: "sernit_catalogo_recurso");

            migrationBuilder.DropIndex(
                name: "ix_ser_catalogo_recurso_valor",
                schema: "smsmarica",
                table: "ser_catalogo_recurso");

            migrationBuilder.DropIndex(
                name: "ux_ser_catalogo_recurso_nome",
                schema: "smsmarica",
                table: "ser_catalogo_recurso");

            migrationBuilder.DropIndex(
                name: "ix_esussg_catalogo_recurso_valor",
                schema: "smsmarica",
                table: "esussg_catalogo_recurso");

            migrationBuilder.DropIndex(
                name: "ux_esussg_catalogo_recurso_nome",
                schema: "smsmarica",
                table: "esussg_catalogo_recurso");

            migrationBuilder.DropColumn(
                name: "rotulo_chave",
                schema: "smsmarica",
                table: "sernit_catalogo_recurso");

            migrationBuilder.DropColumn(
                name: "rotulo_chave",
                schema: "smsmarica",
                table: "ser_catalogo_recurso");

            migrationBuilder.DropColumn(
                name: "rotulo_chave",
                schema: "smsmarica",
                table: "esussg_catalogo_recurso");

            migrationBuilder.CreateIndex(
                name: "ux_sernit_catalogo_recurso",
                schema: "smsmarica",
                table: "sernit_catalogo_recurso",
                columns: new[] { "tipo", "valor" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_ser_catalogo_recurso",
                schema: "smsmarica",
                table: "ser_catalogo_recurso",
                columns: new[] { "tipo", "ambulatorio_estadual", "valor" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_esussg_catalogo_recurso",
                schema: "smsmarica",
                table: "esussg_catalogo_recurso",
                columns: new[] { "tipo", "valor" },
                unique: true);
        }
    }
}
