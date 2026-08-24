using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class PepCursorRetomada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "paciente_cursor_cd",
                schema: "smsmarica",
                table: "pep_sincronizacao_estado",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "paciente_cursor_cd",
                schema: "smsmarica",
                table: "pep_sincronizacao_estado");
        }
    }
}
