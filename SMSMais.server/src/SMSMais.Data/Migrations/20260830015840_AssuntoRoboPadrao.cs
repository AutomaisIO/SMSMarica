using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AssuntoRoboPadrao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "padrao",
                schema: "smsmarica",
                table: "robo_assunto",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_robo_assunto_padrao",
                schema: "smsmarica",
                table: "robo_assunto",
                column: "padrao",
                unique: true,
                filter: "padrao AND excluido_em IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_robo_assunto_padrao",
                schema: "smsmarica",
                table: "robo_assunto");

            migrationBuilder.DropColumn(
                name: "padrao",
                schema: "smsmarica",
                table: "robo_assunto");
        }
    }
}
