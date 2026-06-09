using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class EmailUsuarioOpcional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_usuario_email",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                schema: "smsmarica",
                table: "usuario",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "IX_usuario_email",
                schema: "smsmarica",
                table: "usuario",
                column: "email",
                unique: true,
                filter: "email IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_usuario_email",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                schema: "smsmarica",
                table: "usuario",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuario_email",
                schema: "smsmarica",
                table: "usuario",
                column: "email",
                unique: true);
        }
    }
}
