using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLaudoChecklistBiRads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "estrutura_json",
                schema: "smsmarica",
                table: "laudo_template",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bi_rads",
                schema: "smsmarica",
                table: "laudo",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bi_rads_sugerido",
                schema: "smsmarica",
                table: "laudo",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "respostas_checklist",
                schema: "smsmarica",
                table: "laudo",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_laudo_bi_rads_status",
                schema: "smsmarica",
                table: "laudo",
                columns: new[] { "bi_rads", "status" },
                filter: "bi_rads IS NOT NULL AND excluido = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_laudo_bi_rads_status",
                schema: "smsmarica",
                table: "laudo");

            migrationBuilder.DropColumn(
                name: "estrutura_json",
                schema: "smsmarica",
                table: "laudo_template");

            migrationBuilder.DropColumn(
                name: "bi_rads",
                schema: "smsmarica",
                table: "laudo");

            migrationBuilder.DropColumn(
                name: "bi_rads_sugerido",
                schema: "smsmarica",
                table: "laudo");

            migrationBuilder.DropColumn(
                name: "respostas_checklist",
                schema: "smsmarica",
                table: "laudo");
        }
    }
}
