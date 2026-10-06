using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class IntegracaoElevenLabs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "elevenlabs_configuracao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_url = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    modelo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    api_key_cifrada = table.Column<string>(type: "text", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_elevenlabs_configuracao", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "elevenlabs_configuracao",
                schema: "smsmarica");
        }
    }
}
