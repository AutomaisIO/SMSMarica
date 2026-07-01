using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaCnesUnidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cnes",
                schema: "smsmarica",
                table: "unidade",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_unidade_cnes",
                schema: "smsmarica",
                table: "unidade",
                column: "cnes",
                unique: true,
                filter: "cnes IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_unidade_cnes",
                schema: "smsmarica",
                table: "unidade");

            migrationBuilder.DropColumn(
                name: "cnes",
                schema: "smsmarica",
                table: "unidade");
        }
    }
}
