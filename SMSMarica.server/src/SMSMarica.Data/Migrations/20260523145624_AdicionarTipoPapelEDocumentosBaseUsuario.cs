using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarTipoPapelEDocumentosBaseUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "rg",
                schema: "smsmarica",
                table: "usuario",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sexo",
                schema: "smsmarica",
                table: "usuario",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tipo_papel",
                schema: "smsmarica",
                table: "usuario",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuario_cpf",
                schema: "smsmarica",
                table: "usuario",
                column: "cpf",
                unique: true,
                filter: "cpf IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_usuario_papel_exige_cpf",
                schema: "smsmarica",
                table: "usuario",
                sql: "tipo_papel IS NULL OR cpf IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_usuario_cpf",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropCheckConstraint(
                name: "ck_usuario_papel_exige_cpf",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "rg",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "sexo",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "tipo_papel",
                schema: "smsmarica",
                table: "usuario");
        }
    }
}
