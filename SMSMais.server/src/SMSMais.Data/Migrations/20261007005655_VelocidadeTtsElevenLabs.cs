using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class VelocidadeTtsElevenLabs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "velocidade_tts",
                schema: "smsmarica",
                table: "elevenlabs_configuracao",
                type: "double precision",
                nullable: false,
                defaultValue: 1.1499999999999999);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "velocidade_tts",
                schema: "smsmarica",
                table: "elevenlabs_configuracao");
        }
    }
}
