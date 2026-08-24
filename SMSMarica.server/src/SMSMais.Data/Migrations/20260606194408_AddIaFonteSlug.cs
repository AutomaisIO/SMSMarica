using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIaFonteSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "slug",
                schema: "smsmarica",
                table: "ia_fonte",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ia_fonte_slug",
                schema: "smsmarica",
                table: "ia_fonte",
                column: "slug",
                unique: true,
                filter: "slug IS NOT NULL AND excluido_em IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ia_fonte_slug",
                schema: "smsmarica",
                table: "ia_fonte");

            migrationBuilder.DropColumn(
                name: "slug",
                schema: "smsmarica",
                table: "ia_fonte");
        }
    }
}
