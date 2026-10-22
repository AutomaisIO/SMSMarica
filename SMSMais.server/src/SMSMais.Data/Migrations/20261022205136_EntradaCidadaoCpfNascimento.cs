using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class EntradaCidadaoCpfNascimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cidadao_entrada_bloqueio",
                schema: "smsmarica",
                columns: table => new
                {
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    falhas = table.Column<int>(type: "integer", nullable: false),
                    primeira_falha_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    bloqueado_ate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cidadao_entrada_bloqueio", x => x.cpf);
                });

            migrationBuilder.CreateTable(
                name: "configuracao_app_cidadao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pwa_consultar_esus = table.Column<bool>(type: "boolean", nullable: false),
                    nativo_consultar_esus = table.Column<bool>(type: "boolean", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracao_app_cidadao", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cidadao_entrada_bloqueio",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "configuracao_app_cidadao",
                schema: "smsmarica");
        }
    }
}
