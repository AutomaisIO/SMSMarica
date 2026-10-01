using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class MotoristaCategoriaCnhRegime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "categoria_cnh",
                schema: "smsmarica",
                table: "motorista",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "regime_contratacao",
                schema: "smsmarica",
                table: "motorista",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "categoria_cnh",
                schema: "smsmarica",
                table: "motorista");

            migrationBuilder.DropColumn(
                name: "regime_contratacao",
                schema: "smsmarica",
                table: "motorista");
        }
    }
}
