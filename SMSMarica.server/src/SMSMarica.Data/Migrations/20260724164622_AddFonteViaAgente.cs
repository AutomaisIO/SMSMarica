using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFonteViaAgente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "agente_token_hash",
                schema: "smsmarica",
                table: "ia_fonte",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "via_agente",
                schema: "smsmarica",
                table: "ia_fonte",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "agente_token_hash",
                schema: "smsmarica",
                table: "ia_fonte");

            migrationBuilder.DropColumn(
                name: "via_agente",
                schema: "smsmarica",
                table: "ia_fonte");
        }
    }
}
