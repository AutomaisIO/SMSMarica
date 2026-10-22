using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class TransladoEventoViagem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "translado_evento_viagem",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_local = table.Column<Guid>(type: "uuid", nullable: false),
                    rota_id = table.Column<Guid>(type: "uuid", nullable: false),
                    veiculo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispositivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    motorista_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    sessao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pessoa = table.Column<int>(type: "integer", nullable: true),
                    acompanhante_ordem = table.Column<int>(type: "integer", nullable: true),
                    acompanhante_id = table.Column<Guid>(type: "uuid", nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    precisao_m = table.Column<double>(type: "double precision", nullable: true),
                    ocorrido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recebido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    aplicado = table.Column<bool>(type: "boolean", nullable: false),
                    motivo_nao_aplicado = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translado_evento_viagem", x => x.id);
                    table.ForeignKey(
                        name: "FK_translado_evento_viagem_acompanhante_acompanhante_id",
                        column: x => x.acompanhante_id,
                        principalSchema: "smsmarica",
                        principalTable: "acompanhante",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_translado_evento_viagem_motorista_motorista_id",
                        column: x => x.motorista_id,
                        principalSchema: "smsmarica",
                        principalTable: "motorista",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_translado_evento_viagem_sessao_de_tratamento_sessao_id",
                        column: x => x.sessao_id,
                        principalSchema: "smsmarica",
                        principalTable: "sessao_de_tratamento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_translado_evento_viagem_translado_rota_diaria_rota_id",
                        column: x => x.rota_id,
                        principalSchema: "smsmarica",
                        principalTable: "translado_rota_diaria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_translado_evento_viagem_veiculo_veiculo_id",
                        column: x => x.veiculo_id,
                        principalSchema: "smsmarica",
                        principalTable: "veiculo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_translado_evento_viagem_acompanhante_id",
                schema: "smsmarica",
                table: "translado_evento_viagem",
                column: "acompanhante_id");

            migrationBuilder.CreateIndex(
                name: "IX_translado_evento_viagem_id_local",
                schema: "smsmarica",
                table: "translado_evento_viagem",
                column: "id_local",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_translado_evento_viagem_motorista_id",
                schema: "smsmarica",
                table: "translado_evento_viagem",
                column: "motorista_id");

            migrationBuilder.CreateIndex(
                name: "IX_translado_evento_viagem_rota_id_ocorrido_em",
                schema: "smsmarica",
                table: "translado_evento_viagem",
                columns: new[] { "rota_id", "ocorrido_em" });

            migrationBuilder.CreateIndex(
                name: "IX_translado_evento_viagem_sessao_id",
                schema: "smsmarica",
                table: "translado_evento_viagem",
                column: "sessao_id");

            migrationBuilder.CreateIndex(
                name: "IX_translado_evento_viagem_veiculo_id",
                schema: "smsmarica",
                table: "translado_evento_viagem",
                column: "veiculo_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "translado_evento_viagem",
                schema: "smsmarica");
        }
    }
}
