using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Zap.Data.Migrations
{
    /// <inheritdoc />
    public partial class SegredoEntregaPorWaba : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "segredo_entrega_cifrado",
                schema: "zap",
                table: "waba",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "segredo_entrega_cifrado",
                schema: "zap",
                table: "waba");
        }
    }
}
