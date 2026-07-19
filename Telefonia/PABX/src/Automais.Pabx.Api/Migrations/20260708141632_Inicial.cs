using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Pabx.Api.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "unidade",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Grupo = table.Column<string>(type: "TEXT", nullable: false),
                    Lan = table.Column<string>(type: "TEXT", nullable: false),
                    GatewayMk = table.Column<string>(type: "TEXT", nullable: false),
                    TunnelIp = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Endereco = table.Column<string>(type: "TEXT", nullable: true),
                    Gestor = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unidade", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ramal",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Numero = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    SecretCifrado = table.Column<string>(type: "TEXT", nullable: false),
                    UnidadeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", nullable: true),
                    mac = table.Column<string>(type: "TEXT", maxLength: 12, nullable: true),
                    Marca = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Modelo = table.Column<string>(type: "TEXT", nullable: true),
                    CallerId = table.Column<string>(type: "TEXT", nullable: true),
                    Ativo = table.Column<bool>(type: "INTEGER", nullable: false),
                    Origem = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ramal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ramal_unidade_UnidadeId",
                        column: x => x.UnidadeId,
                        principalTable: "unidade",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "arquivo_gerenciado",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Caminho = table.Column<string>(type: "TEXT", nullable: false),
                    HashSha256 = table.Column<string>(type: "TEXT", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    RamalId = table.Column<int>(type: "INTEGER", nullable: true),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arquivo_gerenciado", x => x.Id);
                    table.ForeignKey(
                        name: "FK_arquivo_gerenciado_ramal_RamalId",
                        column: x => x.RamalId,
                        principalTable: "ramal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_arquivo_gerenciado_Caminho",
                table: "arquivo_gerenciado",
                column: "Caminho",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_arquivo_gerenciado_RamalId",
                table: "arquivo_gerenciado",
                column: "RamalId");

            migrationBuilder.CreateIndex(
                name: "IX_ramal_mac",
                table: "ramal",
                column: "mac",
                unique: true,
                filter: "mac IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ramal_Numero",
                table: "ramal",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ramal_UnidadeId",
                table: "ramal",
                column: "UnidadeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "arquivo_gerenciado");

            migrationBuilder.DropTable(
                name: "ramal");

            migrationBuilder.DropTable(
                name: "unidade");
        }
    }
}
