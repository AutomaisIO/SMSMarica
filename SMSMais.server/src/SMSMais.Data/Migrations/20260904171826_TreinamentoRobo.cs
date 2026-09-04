using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class TreinamentoRobo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "robo_treinamento_item",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    robo_erro_resposta_id = table.Column<Guid>(type: "uuid", nullable: true),
                    conversa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    mensagem_whatsapp_id = table.Column<Guid>(type: "uuid", nullable: true),
                    robo_assunto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    trecho = table.Column<string>(type: "text", nullable: true),
                    contexto_json = table.Column<string>(type: "jsonb", nullable: true),
                    critica = table.Column<string>(type: "text", nullable: false),
                    observacao = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    analise = table.Column<string>(type: "text", nullable: true),
                    analise_json = table.Column<string>(type: "jsonb", nullable: true),
                    modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tokens_entrada = table.Column<long>(type: "bigint", nullable: false),
                    tokens_saida = table.Column<long>(type: "bigint", nullable: false),
                    custo_usd = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    analisado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tentativas_analise = table.Column<int>(type: "integer", nullable: false),
                    erro_mensagem = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_robo_treinamento_item", x => x.id);
                    table.ForeignKey(
                        name: "FK_robo_treinamento_item_conversa_conversa_id",
                        column: x => x.conversa_id,
                        principalSchema: "smsmarica",
                        principalTable: "conversa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_robo_treinamento_item_robo_assunto_robo_assunto_id",
                        column: x => x.robo_assunto_id,
                        principalSchema: "smsmarica",
                        principalTable: "robo_assunto",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_robo_treinamento_item_robo_erro_resposta_robo_erro_resposta~",
                        column: x => x.robo_erro_resposta_id,
                        principalSchema: "smsmarica",
                        principalTable: "robo_erro_resposta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_robo_treinamento_item_whatsapp_mensagem_mensagem_whatsapp_id",
                        column: x => x.mensagem_whatsapp_id,
                        principalSchema: "smsmarica",
                        principalTable: "whatsapp_mensagem",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "robo_treinamento_alteracao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    robo_treinamento_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alvo = table.Column<int>(type: "integer", nullable: false),
                    operacao = table.Column<int>(type: "integer", nullable: false),
                    robo_assunto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alvo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor_anterior_json = table.Column<string>(type: "jsonb", nullable: true),
                    valor_novo_json = table.Column<string>(type: "jsonb", nullable: true),
                    justificativa = table.Column<string>(type: "text", nullable: true),
                    aplicado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    desfeito_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    desfeito_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_robo_treinamento_alteracao", x => x.id);
                    table.ForeignKey(
                        name: "FK_robo_treinamento_alteracao_robo_assunto_robo_assunto_id",
                        column: x => x.robo_assunto_id,
                        principalSchema: "smsmarica",
                        principalTable: "robo_assunto",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_robo_treinamento_alteracao_robo_treinamento_item_robo_trein~",
                        column: x => x.robo_treinamento_item_id,
                        principalSchema: "smsmarica",
                        principalTable: "robo_treinamento_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "robo_treinamento_pendencia",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    robo_treinamento_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    pergunta = table.Column<string>(type: "text", nullable: false),
                    contexto = table.Column<string>(type: "text", nullable: true),
                    opcoes_json = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    resposta = table.Column<string>(type: "text", nullable: true),
                    autorizado = table.Column<bool>(type: "boolean", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    respondido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    respondido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_robo_treinamento_pendencia", x => x.id);
                    table.ForeignKey(
                        name: "FK_robo_treinamento_pendencia_robo_treinamento_item_robo_trein~",
                        column: x => x.robo_treinamento_item_id,
                        principalSchema: "smsmarica",
                        principalTable: "robo_treinamento_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "robo_treinamento_simulacao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    robo_treinamento_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mensagem = table.Column<string>(type: "text", nullable: false),
                    historico_json = table.Column<string>(type: "jsonb", nullable: true),
                    assunto_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    resposta = table.Column<string>(type: "text", nullable: true),
                    chamadas_json = table.Column<string>(type: "jsonb", nullable: true),
                    veredito = table.Column<int>(type: "integer", nullable: true),
                    analise = table.Column<string>(type: "text", nullable: true),
                    tokens_entrada = table.Column<long>(type: "bigint", nullable: false),
                    tokens_saida = table.Column<long>(type: "bigint", nullable: false),
                    custo_usd = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    duracao_ms = table.Column<long>(type: "bigint", nullable: false),
                    automatica = table.Column<bool>(type: "boolean", nullable: false),
                    erro_mensagem = table.Column<string>(type: "text", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_robo_treinamento_simulacao", x => x.id);
                    table.ForeignKey(
                        name: "FK_robo_treinamento_simulacao_robo_treinamento_item_robo_trein~",
                        column: x => x.robo_treinamento_item_id,
                        principalSchema: "smsmarica",
                        principalTable: "robo_treinamento_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_robo_treinamento_alteracao_item",
                schema: "smsmarica",
                table: "robo_treinamento_alteracao",
                column: "robo_treinamento_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_robo_treinamento_alteracao_robo_assunto_id",
                schema: "smsmarica",
                table: "robo_treinamento_alteracao",
                column: "robo_assunto_id");

            migrationBuilder.CreateIndex(
                name: "IX_robo_treinamento_item_conversa_id",
                schema: "smsmarica",
                table: "robo_treinamento_item",
                column: "conversa_id");

            migrationBuilder.CreateIndex(
                name: "IX_robo_treinamento_item_mensagem_whatsapp_id",
                schema: "smsmarica",
                table: "robo_treinamento_item",
                column: "mensagem_whatsapp_id");

            migrationBuilder.CreateIndex(
                name: "IX_robo_treinamento_item_robo_assunto_id",
                schema: "smsmarica",
                table: "robo_treinamento_item",
                column: "robo_assunto_id");

            migrationBuilder.CreateIndex(
                name: "ix_robo_treinamento_status_criado",
                schema: "smsmarica",
                table: "robo_treinamento_item",
                columns: new[] { "status", "criado_em" });

            migrationBuilder.CreateIndex(
                name: "ux_robo_treinamento_erro",
                schema: "smsmarica",
                table: "robo_treinamento_item",
                column: "robo_erro_resposta_id",
                unique: true,
                filter: "robo_erro_resposta_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_robo_treinamento_pendencia_item",
                schema: "smsmarica",
                table: "robo_treinamento_pendencia",
                columns: new[] { "robo_treinamento_item_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_robo_treinamento_simulacao_item",
                schema: "smsmarica",
                table: "robo_treinamento_simulacao",
                columns: new[] { "robo_treinamento_item_id", "criado_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "robo_treinamento_alteracao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "robo_treinamento_pendencia",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "robo_treinamento_simulacao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "robo_treinamento_item",
                schema: "smsmarica");
        }
    }
}
