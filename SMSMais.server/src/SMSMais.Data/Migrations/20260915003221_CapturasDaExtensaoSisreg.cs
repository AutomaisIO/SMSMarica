using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class CapturasDaExtensaoSisreg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sisreg_captura_navegador",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ocorrido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    install_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    versao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    operador_sisreg = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    metodo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    caminho = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    etapa = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    evento = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    escrita = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    conteudo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sisreg_captura_navegador", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_captura_criado",
                schema: "smsmarica",
                table: "sisreg_captura_navegador",
                column: "criado_em");

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_captura_evento",
                schema: "smsmarica",
                table: "sisreg_captura_navegador",
                column: "evento",
                filter: "evento IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_captura_install",
                schema: "smsmarica",
                table: "sisreg_captura_navegador",
                columns: new[] { "install_id", "criado_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sisreg_captura_navegador",
                schema: "smsmarica");
        }
    }
}
