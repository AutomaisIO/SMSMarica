using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Pabx.Api.Migrations
{
    /// <summary>
    /// Tipo (físico/softphone), configuração SIP por ramal e dono externo. Os defaults reproduzem
    /// exatamente o bloco que o gerador escrevia fixo, então os ramais existentes não mudam.
    /// </summary>
    public partial class RamalTipoConfigDono : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CallLimit",
                table: "ramal",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Codecs",
                table: "ramal",
                type: "TEXT",
                maxLength: 120,
                nullable: false,
                defaultValue: "alaw,ulaw,gsm");

            migrationBuilder.AddColumn<string>(
                name: "Contexto",
                table: "ramal",
                type: "TEXT",
                maxLength: 80,
                nullable: false,
                defaultValue: "PLANO_HOSPITAIS");

            migrationBuilder.AddColumn<string>(
                name: "DonoId",
                table: "ramal",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DonoSistema",
                table: "ramal",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tipo",
                table: "ramal",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Fisico");

            migrationBuilder.CreateIndex(
                name: "IX_ramal_DonoSistema_DonoId",
                table: "ramal",
                columns: new[] { "DonoSistema", "DonoId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ramal_DonoSistema_DonoId",
                table: "ramal");

            migrationBuilder.DropColumn(
                name: "CallLimit",
                table: "ramal");

            migrationBuilder.DropColumn(
                name: "Codecs",
                table: "ramal");

            migrationBuilder.DropColumn(
                name: "Contexto",
                table: "ramal");

            migrationBuilder.DropColumn(
                name: "DonoId",
                table: "ramal");

            migrationBuilder.DropColumn(
                name: "DonoSistema",
                table: "ramal");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "ramal");
        }
    }
}
