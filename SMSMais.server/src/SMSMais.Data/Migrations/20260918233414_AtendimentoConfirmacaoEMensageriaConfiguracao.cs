using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AtendimentoConfirmacaoEMensageriaConfiguracao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "atendimento_confirmacao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    atendente_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    iniciado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    encerrado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_atendimento_confirmacao", x => x.id);
                    table.ForeignKey(
                        name: "FK_atendimento_confirmacao_solicitacao_solicitacao_id",
                        column: x => x.solicitacao_id,
                        principalSchema: "smsmarica",
                        principalTable: "solicitacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_atendimento_confirmacao_usuario_atendente_usuario_id",
                        column: x => x.atendente_usuario_id,
                        principalSchema: "smsmarica",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mensageria_configuracao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarifa_utility_usd = table.Column<decimal>(type: "numeric(10,5)", precision: 10, scale: 5, nullable: true),
                    tarifa_marketing_usd = table.Column<decimal>(type: "numeric(10,5)", precision: 10, scale: 5, nullable: true),
                    tarifa_authentication_usd = table.Column<decimal>(type: "numeric(10,5)", precision: 10, scale: 5, nullable: true),
                    templates_categorias_json = table.Column<string>(type: "jsonb", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mensageria_configuracao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "atendimento_confirmacao_evento",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    atendimento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    ator_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    de_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    para_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ocorrido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_atendimento_confirmacao_evento", x => x.id);
                    table.ForeignKey(
                        name: "FK_atendimento_confirmacao_evento_atendimento_confirmacao_aten~",
                        column: x => x.atendimento_id,
                        principalSchema: "smsmarica",
                        principalTable: "atendimento_confirmacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_atendimento_confirmacao_atendente_usuario_id_situacao",
                schema: "smsmarica",
                table: "atendimento_confirmacao",
                columns: new[] { "atendente_usuario_id", "situacao" });

            migrationBuilder.CreateIndex(
                name: "ux_atendimento_confirmacao_ativo",
                schema: "smsmarica",
                table: "atendimento_confirmacao",
                column: "solicitacao_id",
                unique: true,
                filter: "encerrado_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_atendimento_confirmacao_evento_atendimento_id_ocorrido_em",
                schema: "smsmarica",
                table: "atendimento_confirmacao_evento",
                columns: new[] { "atendimento_id", "ocorrido_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "atendimento_confirmacao_evento",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "mensageria_configuracao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "atendimento_confirmacao",
                schema: "smsmarica");
        }
    }
}
