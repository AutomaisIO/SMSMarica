using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class OcorrenciaVeiculo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rastreamento_ocorrencia",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    veiculo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispositivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_local = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    precisao_m = table.Column<double>(type: "double precision", nullable: true),
                    rota_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ocorrida_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recebida_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    atendida_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atendida_por = table.Column<Guid>(type: "uuid", nullable: true),
                    encerrada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    encerrada_por = table.Column<Guid>(type: "uuid", nullable: true),
                    observacao_central = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rastreamento_ocorrencia", x => x.id);
                    table.ForeignKey(
                        name: "FK_rastreamento_ocorrencia_translado_rota_diaria_rota_id",
                        column: x => x.rota_id,
                        principalSchema: "smsmarica",
                        principalTable: "translado_rota_diaria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_rastreamento_ocorrencia_veiculo_veiculo_id",
                        column: x => x.veiculo_id,
                        principalSchema: "smsmarica",
                        principalTable: "veiculo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rastreamento_ocorrencia_id_local",
                schema: "smsmarica",
                table: "rastreamento_ocorrencia",
                column: "id_local",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rastreamento_ocorrencia_rota_id",
                schema: "smsmarica",
                table: "rastreamento_ocorrencia",
                column: "rota_id");

            migrationBuilder.CreateIndex(
                name: "IX_rastreamento_ocorrencia_situacao_recebida_em",
                schema: "smsmarica",
                table: "rastreamento_ocorrencia",
                columns: new[] { "situacao", "recebida_em" });

            migrationBuilder.CreateIndex(
                name: "IX_rastreamento_ocorrencia_veiculo_id",
                schema: "smsmarica",
                table: "rastreamento_ocorrencia",
                column: "veiculo_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rastreamento_ocorrencia",
                schema: "smsmarica");
        }
    }
}
