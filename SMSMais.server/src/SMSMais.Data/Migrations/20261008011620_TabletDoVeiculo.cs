using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class TabletDoVeiculo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rastreamento_posicao_veiculo",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    veiculo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispositivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    velocidade_kmh = table.Column<double>(type: "double precision", nullable: true),
                    rumo = table.Column<double>(type: "double precision", nullable: true),
                    precisao_m = table.Column<double>(type: "double precision", nullable: true),
                    capturado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rastreamento_posicao_veiculo", x => x.id);
                    table.ForeignKey(
                        name: "FK_rastreamento_posicao_veiculo_veiculo_veiculo_id",
                        column: x => x.veiculo_id,
                        principalSchema: "smsmarica",
                        principalTable: "veiculo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "veiculo_dispositivo",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    veiculo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    codigo_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    codigo_expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modelo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    identificador = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ativado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ultimo_contato_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_veiculo_dispositivo", x => x.id);
                    table.ForeignKey(
                        name: "FK_veiculo_dispositivo_veiculo_veiculo_id",
                        column: x => x.veiculo_id,
                        principalSchema: "smsmarica",
                        principalTable: "veiculo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rastreamento_posicao_veiculo_veiculo_id_capturado_em",
                schema: "smsmarica",
                table: "rastreamento_posicao_veiculo",
                columns: new[] { "veiculo_id", "capturado_em" });

            migrationBuilder.CreateIndex(
                name: "IX_veiculo_dispositivo_codigo_hash",
                schema: "smsmarica",
                table: "veiculo_dispositivo",
                column: "codigo_hash");

            migrationBuilder.CreateIndex(
                name: "IX_veiculo_dispositivo_token_hash",
                schema: "smsmarica",
                table: "veiculo_dispositivo",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_veiculo_dispositivo_veiculo_id",
                schema: "smsmarica",
                table: "veiculo_dispositivo",
                column: "veiculo_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rastreamento_posicao_veiculo",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "veiculo_dispositivo",
                schema: "smsmarica");
        }
    }
}
