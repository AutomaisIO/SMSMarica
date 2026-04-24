using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssentoBloqueadoExcluido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_veiculo_assento_fileira_id_numero",
                schema: "smsmarica",
                table: "veiculo_assento");

            migrationBuilder.AddColumn<bool>(
                name: "bloqueado",
                schema: "smsmarica",
                table: "veiculo_assento",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "excluido",
                schema: "smsmarica",
                table: "veiculo_assento",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_veiculo_assento_fileira_id_numero",
                schema: "smsmarica",
                table: "veiculo_assento",
                columns: new[] { "fileira_id", "numero" },
                unique: true,
                filter: "excluido = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_veiculo_assento_fileira_id_numero",
                schema: "smsmarica",
                table: "veiculo_assento");

            migrationBuilder.DropColumn(
                name: "bloqueado",
                schema: "smsmarica",
                table: "veiculo_assento");

            migrationBuilder.DropColumn(
                name: "excluido",
                schema: "smsmarica",
                table: "veiculo_assento");

            migrationBuilder.CreateIndex(
                name: "IX_veiculo_assento_fileira_id_numero",
                schema: "smsmarica",
                table: "veiculo_assento",
                columns: new[] { "fileira_id", "numero" },
                unique: true);
        }
    }
}
