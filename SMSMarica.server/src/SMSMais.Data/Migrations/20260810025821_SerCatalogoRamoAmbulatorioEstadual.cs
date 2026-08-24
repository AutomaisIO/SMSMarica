using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class SerCatalogoRamoAmbulatorioEstadual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_ser_catalogo_recurso",
                schema: "smsmarica",
                table: "ser_catalogo_recurso");

            migrationBuilder.AddColumn<bool>(
                name: "ambulatorio_estadual",
                schema: "smsmarica",
                table: "ser_solicitacao_rascunho",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ambulatorio_estadual",
                schema: "smsmarica",
                table: "ser_catalogo_recurso",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ux_ser_catalogo_recurso",
                schema: "smsmarica",
                table: "ser_catalogo_recurso",
                columns: new[] { "tipo", "ambulatorio_estadual", "valor" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_ser_catalogo_recurso",
                schema: "smsmarica",
                table: "ser_catalogo_recurso");

            migrationBuilder.DropColumn(
                name: "ambulatorio_estadual",
                schema: "smsmarica",
                table: "ser_solicitacao_rascunho");

            migrationBuilder.DropColumn(
                name: "ambulatorio_estadual",
                schema: "smsmarica",
                table: "ser_catalogo_recurso");

            migrationBuilder.CreateIndex(
                name: "ux_ser_catalogo_recurso",
                schema: "smsmarica",
                table: "ser_catalogo_recurso",
                columns: new[] { "tipo", "valor" },
                unique: true);
        }
    }
}
