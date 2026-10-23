using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompartilhamentoLocalizacaoPaciente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "translado_compartilhamento_localizacao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sessao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trecho = table.Column<int>(type: "integer", nullable: false),
                    cidadao_sessao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    canal = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    iniciado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    encerrado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    motivo_encerramento = table.Column<int>(type: "integer", nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    precisao_m = table.Column<double>(type: "double precision", nullable: true),
                    simulada = table.Column<bool>(type: "boolean", nullable: false),
                    capturada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    recebida_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translado_compartilhamento_localizacao", x => x.id);
                    table.ForeignKey(
                        name: "FK_translado_compartilhamento_localizacao_sessao_de_tratamento~",
                        column: x => x.sessao_id,
                        principalSchema: "smsmarica",
                        principalTable: "sessao_de_tratamento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_translado_compartilhamento_localizacao_encerrado_em",
                schema: "smsmarica",
                table: "translado_compartilhamento_localizacao",
                column: "encerrado_em");

            migrationBuilder.CreateIndex(
                name: "IX_translado_compartilhamento_localizacao_sessao_id_trecho",
                schema: "smsmarica",
                table: "translado_compartilhamento_localizacao",
                columns: new[] { "sessao_id", "trecho" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "translado_compartilhamento_localizacao",
                schema: "smsmarica");
        }
    }
}
