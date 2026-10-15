using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class MotoristaBiometriaFacial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "motorista_referencia_facial",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    motorista_id = table.Column<Guid>(type: "uuid", nullable: false),
                    foto_base64 = table.Column<string>(type: "text", nullable: false),
                    foto_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    dispositivo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    veiculo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    termo_versao = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    termo_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    cadastrada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_motorista_referencia_facial", x => x.id);
                    table.ForeignKey(
                        name: "FK_motorista_referencia_facial_motorista_motorista_id",
                        column: x => x.motorista_id,
                        principalSchema: "smsmarica",
                        principalTable: "motorista",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_motorista_referencia_facial_veiculo_veiculo_id",
                        column: x => x.veiculo_id,
                        principalSchema: "smsmarica",
                        principalTable: "veiculo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "rastreamento_liberacao_motorista",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    veiculo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    motorista_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    criada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criada_por = table.Column<Guid>(type: "uuid", nullable: true),
                    expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tentativas_erradas = table.Column<int>(type: "integer", nullable: false),
                    usada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usada_por_dispositivo_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rastreamento_liberacao_motorista", x => x.id);
                    table.ForeignKey(
                        name: "FK_rastreamento_liberacao_motorista_motorista_motorista_id",
                        column: x => x.motorista_id,
                        principalSchema: "smsmarica",
                        principalTable: "motorista",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_rastreamento_liberacao_motorista_veiculo_veiculo_id",
                        column: x => x.veiculo_id,
                        principalSchema: "smsmarica",
                        principalTable: "veiculo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rastreamento_verificacao_facial",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_local = table.Column<Guid>(type: "uuid", nullable: false),
                    veiculo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispositivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    motorista_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rota_id = table.Column<Guid>(type: "uuid", nullable: true),
                    momento = table.Column<int>(type: "integer", nullable: false),
                    meio = table.Column<int>(type: "integer", nullable: false),
                    aprovada = table.Column<bool>(type: "boolean", nullable: false),
                    nota = table.Column<double>(type: "double precision", nullable: true),
                    limiar = table.Column<double>(type: "double precision", nullable: true),
                    prova_de_vida = table.Column<bool>(type: "boolean", nullable: false),
                    modelo_versao = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    precisao_m = table.Column<double>(type: "double precision", nullable: true),
                    capturada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recebida_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    selfie_base64 = table.Column<string>(type: "text", nullable: true),
                    selfie_apagada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    liberacao_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rastreamento_verificacao_facial", x => x.id);
                    table.ForeignKey(
                        name: "FK_rastreamento_verificacao_facial_motorista_motorista_id",
                        column: x => x.motorista_id,
                        principalSchema: "smsmarica",
                        principalTable: "motorista",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_rastreamento_verificacao_facial_rastreamento_liberacao_moto~",
                        column: x => x.liberacao_id,
                        principalSchema: "smsmarica",
                        principalTable: "rastreamento_liberacao_motorista",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_rastreamento_verificacao_facial_translado_rota_diaria_rota_~",
                        column: x => x.rota_id,
                        principalSchema: "smsmarica",
                        principalTable: "translado_rota_diaria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_rastreamento_verificacao_facial_veiculo_veiculo_id",
                        column: x => x.veiculo_id,
                        principalSchema: "smsmarica",
                        principalTable: "veiculo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_motorista_referencia_facial_motorista_id",
                schema: "smsmarica",
                table: "motorista_referencia_facial",
                column: "motorista_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_motorista_referencia_facial_veiculo_id",
                schema: "smsmarica",
                table: "motorista_referencia_facial",
                column: "veiculo_id");

            migrationBuilder.CreateIndex(
                name: "IX_rastreamento_liberacao_motorista_motorista_id",
                schema: "smsmarica",
                table: "rastreamento_liberacao_motorista",
                column: "motorista_id");

            migrationBuilder.CreateIndex(
                name: "IX_rastreamento_liberacao_motorista_veiculo_id_criada_em",
                schema: "smsmarica",
                table: "rastreamento_liberacao_motorista",
                columns: new[] { "veiculo_id", "criada_em" });

            migrationBuilder.CreateIndex(
                name: "IX_rastreamento_verificacao_facial_id_local",
                schema: "smsmarica",
                table: "rastreamento_verificacao_facial",
                column: "id_local",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rastreamento_verificacao_facial_liberacao_id",
                schema: "smsmarica",
                table: "rastreamento_verificacao_facial",
                column: "liberacao_id");

            migrationBuilder.CreateIndex(
                name: "IX_rastreamento_verificacao_facial_motorista_id_recebida_em",
                schema: "smsmarica",
                table: "rastreamento_verificacao_facial",
                columns: new[] { "motorista_id", "recebida_em" });

            migrationBuilder.CreateIndex(
                name: "IX_rastreamento_verificacao_facial_rota_id",
                schema: "smsmarica",
                table: "rastreamento_verificacao_facial",
                column: "rota_id");

            migrationBuilder.CreateIndex(
                name: "IX_rastreamento_verificacao_facial_veiculo_id_recebida_em",
                schema: "smsmarica",
                table: "rastreamento_verificacao_facial",
                columns: new[] { "veiculo_id", "recebida_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "motorista_referencia_facial",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "rastreamento_verificacao_facial",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "rastreamento_liberacao_motorista",
                schema: "smsmarica");
        }
    }
}
