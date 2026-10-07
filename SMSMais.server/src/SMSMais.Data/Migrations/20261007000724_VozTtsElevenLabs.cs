using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class VozTtsElevenLabs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "modelo_tts",
                schema: "smsmarica",
                table: "elevenlabs_configuracao",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "voz_id",
                schema: "smsmarica",
                table: "elevenlabs_configuracao",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "modelo_tts",
                schema: "smsmarica",
                table: "elevenlabs_configuracao");

            migrationBuilder.DropColumn(
                name: "voz_id",
                schema: "smsmarica",
                table: "elevenlabs_configuracao");
        }
    }
}
