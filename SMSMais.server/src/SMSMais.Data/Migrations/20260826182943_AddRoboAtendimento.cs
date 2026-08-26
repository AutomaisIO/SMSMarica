using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoboAtendimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "janela_aberta_em",
                schema: "smsmarica",
                table: "conversa",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "robo_assunto_id",
                schema: "smsmarica",
                table: "conversa",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "robo_interacoes_na_janela",
                schema: "smsmarica",
                table: "conversa",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "robo_assunto",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    instrucoes_persona = table.Column<string>(type: "text", nullable: false),
                    modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    horario_inicio = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    horario_fim = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    dias_semana = table.Column<int>(type: "integer", nullable: true),
                    max_interacoes_sem_resolver = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    limiar_confianca = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.59999999999999998),
                    escalonamento_unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_robo_assunto", x => x.id);
                    table.ForeignKey(
                        name: "FK_robo_assunto_unidade_escalonamento_unidade_id",
                        column: x => x.escalonamento_unidade_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "robo_configuracao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    modelo_padrao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    nome_exibicao = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    mensagem_handoff = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    mensagem_fora_horario = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_robo_configuracao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "robo_acao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    robo_assunto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    comando = table.Column<int>(type: "integer", nullable: false),
                    entrada_json = table.Column<string>(type: "jsonb", nullable: true),
                    resultado_json = table.Column<string>(type: "jsonb", nullable: true),
                    sucesso = table.Column<bool>(type: "boolean", nullable: false),
                    idempotencia_chave = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ocorrido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_robo_acao", x => x.id);
                    table.ForeignKey(
                        name: "FK_robo_acao_conversa_conversa_id",
                        column: x => x.conversa_id,
                        principalSchema: "smsmarica",
                        principalTable: "conversa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_robo_acao_robo_assunto_robo_assunto_id",
                        column: x => x.robo_assunto_id,
                        principalSchema: "smsmarica",
                        principalTable: "robo_assunto",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "robo_assunto_comando",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    robo_assunto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comando = table.Column<int>(type: "integer", nullable: false),
                    habilitado = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_robo_assunto_comando", x => x.id);
                    table.ForeignKey(
                        name: "FK_robo_assunto_comando_robo_assunto_robo_assunto_id",
                        column: x => x.robo_assunto_id,
                        principalSchema: "smsmarica",
                        principalTable: "robo_assunto",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "robo_assunto_condicao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    robo_assunto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    valor = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ordem = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_robo_assunto_condicao", x => x.id);
                    table.ForeignKey(
                        name: "FK_robo_assunto_condicao_robo_assunto_robo_assunto_id",
                        column: x => x.robo_assunto_id,
                        principalSchema: "smsmarica",
                        principalTable: "robo_assunto",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "robo_assunto_treino",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    robo_assunto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    conteudo = table.Column<string>(type: "text", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_robo_assunto_treino", x => x.id);
                    table.ForeignKey(
                        name: "FK_robo_assunto_treino_robo_assunto_robo_assunto_id",
                        column: x => x.robo_assunto_id,
                        principalSchema: "smsmarica",
                        principalTable: "robo_assunto",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "robo_tarefa",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mensagem_whatsapp_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    robo_assunto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    session_id_aiengine = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    confianca_ultima = table.Column<double>(type: "double precision", nullable: true),
                    tentativas = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    proxima_tentativa_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    erro = table.Column<string>(type: "text", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_robo_tarefa", x => x.id);
                    table.ForeignKey(
                        name: "FK_robo_tarefa_conversa_conversa_id",
                        column: x => x.conversa_id,
                        principalSchema: "smsmarica",
                        principalTable: "conversa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_robo_tarefa_robo_assunto_robo_assunto_id",
                        column: x => x.robo_assunto_id,
                        principalSchema: "smsmarica",
                        principalTable: "robo_assunto",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_robo_tarefa_whatsapp_mensagem_mensagem_whatsapp_id",
                        column: x => x.mensagem_whatsapp_id,
                        principalSchema: "smsmarica",
                        principalTable: "whatsapp_mensagem",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_conversa_robo_assunto_id",
                schema: "smsmarica",
                table: "conversa",
                column: "robo_assunto_id");

            migrationBuilder.CreateIndex(
                name: "ix_robo_acao_conversa",
                schema: "smsmarica",
                table: "robo_acao",
                column: "conversa_id");

            migrationBuilder.CreateIndex(
                name: "IX_robo_acao_robo_assunto_id",
                schema: "smsmarica",
                table: "robo_acao",
                column: "robo_assunto_id");

            migrationBuilder.CreateIndex(
                name: "ux_robo_acao_idempotencia",
                schema: "smsmarica",
                table: "robo_acao",
                column: "idempotencia_chave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_robo_assunto_escalonamento_unidade_id",
                schema: "smsmarica",
                table: "robo_assunto",
                column: "escalonamento_unidade_id");

            migrationBuilder.CreateIndex(
                name: "ix_robo_assunto_ordem",
                schema: "smsmarica",
                table: "robo_assunto",
                column: "ordem",
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_robo_assunto_nome",
                schema: "smsmarica",
                table: "robo_assunto",
                column: "nome",
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_robo_assunto_comando_robo_assunto_id_comando",
                schema: "smsmarica",
                table: "robo_assunto_comando",
                columns: new[] { "robo_assunto_id", "comando" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_robo_assunto_condicao_robo_assunto_id_ordem",
                schema: "smsmarica",
                table: "robo_assunto_condicao",
                columns: new[] { "robo_assunto_id", "ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_robo_assunto_treino_robo_assunto_id_ordem",
                schema: "smsmarica",
                table: "robo_assunto_treino",
                columns: new[] { "robo_assunto_id", "ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_robo_tarefa_conversa_id",
                schema: "smsmarica",
                table: "robo_tarefa",
                column: "conversa_id");

            migrationBuilder.CreateIndex(
                name: "IX_robo_tarefa_robo_assunto_id",
                schema: "smsmarica",
                table: "robo_tarefa",
                column: "robo_assunto_id");

            migrationBuilder.CreateIndex(
                name: "ix_robo_tarefa_status_proxima",
                schema: "smsmarica",
                table: "robo_tarefa",
                columns: new[] { "status", "proxima_tentativa_em" });

            migrationBuilder.CreateIndex(
                name: "ux_robo_tarefa_mensagem",
                schema: "smsmarica",
                table: "robo_tarefa",
                column: "mensagem_whatsapp_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_conversa_robo_assunto_robo_assunto_id",
                schema: "smsmarica",
                table: "conversa",
                column: "robo_assunto_id",
                principalSchema: "smsmarica",
                principalTable: "robo_assunto",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_conversa_robo_assunto_robo_assunto_id",
                schema: "smsmarica",
                table: "conversa");

            migrationBuilder.DropTable(
                name: "robo_acao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "robo_assunto_comando",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "robo_assunto_condicao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "robo_assunto_treino",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "robo_configuracao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "robo_tarefa",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "robo_assunto",
                schema: "smsmarica");

            migrationBuilder.DropIndex(
                name: "IX_conversa_robo_assunto_id",
                schema: "smsmarica",
                table: "conversa");

            migrationBuilder.DropColumn(
                name: "janela_aberta_em",
                schema: "smsmarica",
                table: "conversa");

            migrationBuilder.DropColumn(
                name: "robo_assunto_id",
                schema: "smsmarica",
                table: "conversa");

            migrationBuilder.DropColumn(
                name: "robo_interacoes_na_janela",
                schema: "smsmarica",
                table: "conversa");
        }
    }
}
