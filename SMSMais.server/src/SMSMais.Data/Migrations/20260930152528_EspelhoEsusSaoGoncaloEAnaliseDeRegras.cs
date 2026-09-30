using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class EspelhoEsusSaoGoncaloEAnaliseDeRegras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_regulacao_solicitacao_um_espelho",
                schema: "smsmarica",
                table: "regulacao_solicitacao");

            migrationBuilder.AddColumn<Guid>(
                name: "esussg_solicitacao_id",
                schema: "smsmarica",
                table: "regulacao_solicitacao",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "esussg_catalogo_recurso_id",
                schema: "smsmarica",
                table: "regulacao_procedimento_origem",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "esussg_catalogo_recurso",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    valor = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    rotulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sincronizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_esussg_catalogo_recurso", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "esussg_solicitacao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_esussg = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    recurso = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    codigo_interno = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    subprocedimentos = table.Column<string>(type: "text", nullable: true),
                    data_solicitacao = table.Column<DateOnly>(type: "date", nullable: true),
                    data_entrada_fila = table.Column<DateOnly>(type: "date", nullable: true),
                    prioridade = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    prioridade_cor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    pendencia = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    posicao_fila = table.Column<int>(type: "integer", nullable: true),
                    ordem_entrada = table.Column<int>(type: "integer", nullable: true),
                    profissional_solicitante = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    unidade_solicitante = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    usuario_inclusao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    regulador = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    pessoa_id_esus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    paciente_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    cns = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    data_nascimento = table.Column<DateOnly>(type: "date", nullable: true),
                    sexo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    nome_mae = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    telefone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    celular = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    municipio_paciente = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    bairro = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    unidade_executora = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    cnes_executora = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    setor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    local = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    data_agendada = table.Column<DateOnly>(type: "date", nullable: true),
                    data_hora_agendada_texto = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    usuario_agendamento = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    agendamento_cadastrado_em = table.Column<DateOnly>(type: "date", nullable: true),
                    data_saida_fila = table.Column<DateOnly>(type: "date", nullable: true),
                    comprovante_impresso = table.Column<bool>(type: "boolean", nullable: true),
                    agendado_tfd = table.Column<bool>(type: "boolean", nullable: true),
                    notificacao_tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    notificacao_entrega = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    notificacao_resposta = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    paciente_conciliar_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sincronizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    visto_na_fila_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    visto_nos_agendados_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    eventos_count = table.Column<int>(type: "integer", nullable: false),
                    ultimo_evento_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    situacao_mudou_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    situacao_anterior = table.Column<int>(type: "integer", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_esussg_solicitacao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "esussg_varredura_execucao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    modo = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    disparo = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    status = table.Column<int>(type: "integer", nullable: false),
                    janela_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    janela_fim = table.Column<DateOnly>(type: "date", nullable: false),
                    requisicoes = table.Column<int>(type: "integer", nullable: false),
                    na_fila = table.Column<int>(type: "integer", nullable: false),
                    agendados_lidos = table.Column<int>(type: "integer", nullable: false),
                    solicitacoes_novas = table.Column<int>(type: "integer", nullable: false),
                    solicitacoes_atualizadas = table.Column<int>(type: "integer", nullable: false),
                    mudancas_situacao = table.Column<int>(type: "integer", nullable: false),
                    saidas_da_fila = table.Column<int>(type: "integer", nullable: false),
                    eventos_novos = table.Column<int>(type: "integer", nullable: false),
                    gatilhos_gerados = table.Column<int>(type: "integer", nullable: false),
                    meses_incompletos = table.Column<int>(type: "integer", nullable: false),
                    fase = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    cursor_mes = table.Column<DateOnly>(type: "date", nullable: true),
                    retomadas = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    retomada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ultimo_sinal_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    mensagem_erro = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    iniciado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finalizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    duracao_segundos = table.Column<int>(type: "integer", nullable: true),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    criado_por_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_esussg_varredura_execucao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "regulacao_analise_espelho",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sistema = table.Column<int>(type: "integer", nullable: false),
                    espelho_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_externo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    procedimento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    veredito = table.Column<int>(type: "integer", nullable: false),
                    bloqueios = table.Column<int>(type: "integer", nullable: false),
                    ressalvas = table.Column<int>(type: "integer", nullable: false),
                    avisos = table.Column<int>(type: "integer", nullable: false),
                    perguntas_pendentes = table.Column<int>(type: "integer", nullable: false),
                    documentos_pendentes = table.Column<int>(type: "integer", nullable: false),
                    resumo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    alertas_json = table.Column<string>(type: "jsonb", nullable: false),
                    entrada_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    analisado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulacao_analise_espelho", x => x.id);
                    table.ForeignKey(
                        name: "FK_regulacao_analise_espelho_regulacao_procedimento_procedimen~",
                        column: x => x.procedimento_id,
                        principalSchema: "smsmarica",
                        principalTable: "regulacao_procedimento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "esussg_evento",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    esussg_solicitacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_evento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    evento = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    tipo_evento = table.Column<int>(type: "integer", nullable: false),
                    followup_categoria = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    followup_regras_hash = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    estado_anterior = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    estado_atual = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    central_regulacao = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    unidade_executora = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    usuario = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    lotacao_evento = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    observacao = table.Column<string>(type: "text", nullable: true),
                    capturado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_esussg_evento", x => x.id);
                    table.ForeignKey(
                        name: "FK_esussg_evento_esussg_solicitacao_esussg_solicitacao_id",
                        column: x => x.esussg_solicitacao_id,
                        principalSchema: "smsmarica",
                        principalTable: "esussg_solicitacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "esussg_gatilho",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    esussg_solicitacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_esussg = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    chave_evento = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    situacao_anterior = table.Column<int>(type: "integer", nullable: true),
                    situacao_atual = table.Column<int>(type: "integer", nullable: true),
                    payload_json = table.Column<string>(type: "jsonb", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    processado_por = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_esussg_gatilho", x => x.id);
                    table.ForeignKey(
                        name: "FK_esussg_gatilho_esussg_solicitacao_esussg_solicitacao_id",
                        column: x => x.esussg_solicitacao_id,
                        principalSchema: "smsmarica",
                        principalTable: "esussg_solicitacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "esussg_varredura_falha",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    execucao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    tipo_recurso = table.Column<int>(type: "integer", nullable: true),
                    mes = table.Column<DateOnly>(type: "date", nullable: true),
                    mensagem = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    detalhe = table.Column<string>(type: "text", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_esussg_varredura_falha", x => x.id);
                    table.ForeignKey(
                        name: "FK_esussg_varredura_falha_esussg_varredura_execucao_execucao_id",
                        column: x => x.execucao_id,
                        principalSchema: "smsmarica",
                        principalTable: "esussg_varredura_execucao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_regulacao_solicitacao_espelho_esussg",
                schema: "smsmarica",
                table: "regulacao_solicitacao",
                column: "esussg_solicitacao_id",
                unique: true,
                filter: "esussg_solicitacao_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_regulacao_solicitacao_um_espelho",
                schema: "smsmarica",
                table: "regulacao_solicitacao",
                sql: "((solicitacao_id IS NOT NULL)::int + (ser_solicitacao_id IS NOT NULL)::int + (sernit_solicitacao_id IS NOT NULL)::int + (esussg_solicitacao_id IS NOT NULL)::int) <= 1");

            migrationBuilder.CreateIndex(
                name: "IX_regulacao_procedimento_origem_esussg_catalogo_recurso_id",
                schema: "smsmarica",
                table: "regulacao_procedimento_origem",
                column: "esussg_catalogo_recurso_id");

            migrationBuilder.CreateIndex(
                name: "ux_esussg_catalogo_recurso",
                schema: "smsmarica",
                table: "esussg_catalogo_recurso",
                columns: new[] { "tipo", "valor" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_esussg_evento_solicitacao_data",
                schema: "smsmarica",
                table: "esussg_evento",
                columns: new[] { "esussg_solicitacao_id", "data_evento" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_esussg_evento_usuario_data",
                schema: "smsmarica",
                table: "esussg_evento",
                columns: new[] { "usuario", "data_evento" });

            migrationBuilder.CreateIndex(
                name: "ux_esussg_evento_solicitacao_data_evento",
                schema: "smsmarica",
                table: "esussg_evento",
                columns: new[] { "esussg_solicitacao_id", "data_evento", "evento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_esussg_gatilho_pendente",
                schema: "smsmarica",
                table: "esussg_gatilho",
                columns: new[] { "tipo", "criado_em" },
                filter: "processado_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_esussg_gatilho_solicitacao_tipo_chave",
                schema: "smsmarica",
                table: "esussg_gatilho",
                columns: new[] { "esussg_solicitacao_id", "tipo", "chave_evento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_esussg_solicitacao_cns",
                schema: "smsmarica",
                table: "esussg_solicitacao",
                column: "cns");

            migrationBuilder.CreateIndex(
                name: "ix_esussg_solicitacao_cpf",
                schema: "smsmarica",
                table: "esussg_solicitacao",
                column: "cpf");

            migrationBuilder.CreateIndex(
                name: "ix_esussg_solicitacao_data_agendada",
                schema: "smsmarica",
                table: "esussg_solicitacao",
                column: "data_agendada");

            migrationBuilder.CreateIndex(
                name: "ix_esussg_solicitacao_paciente_conciliar",
                schema: "smsmarica",
                table: "esussg_solicitacao",
                column: "paciente_conciliar_em",
                filter: "paciente_conciliar_em IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_esussg_solicitacao_paciente_id",
                schema: "smsmarica",
                table: "esussg_solicitacao",
                column: "paciente_id",
                filter: "paciente_id IS NOT NULL AND excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_esussg_solicitacao_situacao_entrada",
                schema: "smsmarica",
                table: "esussg_solicitacao",
                columns: new[] { "situacao", "data_entrada_fila" });

            migrationBuilder.CreateIndex(
                name: "ux_esussg_solicitacao_tipo_id",
                schema: "smsmarica",
                table: "esussg_solicitacao",
                columns: new[] { "tipo", "id_esussg" },
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_esussg_varredura_execucao_iniciado",
                schema: "smsmarica",
                table: "esussg_varredura_execucao",
                column: "iniciado_em",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_esussg_varredura_execucao_status",
                schema: "smsmarica",
                table: "esussg_varredura_execucao",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_esussg_varredura_falha_execucao_tipo",
                schema: "smsmarica",
                table: "esussg_varredura_falha",
                columns: new[] { "execucao_id", "tipo" });

            migrationBuilder.CreateIndex(
                name: "IX_regulacao_analise_espelho_procedimento_id",
                schema: "smsmarica",
                table: "regulacao_analise_espelho",
                column: "procedimento_id");

            migrationBuilder.CreateIndex(
                name: "ix_regulacao_analise_espelho_sistema_veredito",
                schema: "smsmarica",
                table: "regulacao_analise_espelho",
                columns: new[] { "sistema", "veredito" });

            migrationBuilder.CreateIndex(
                name: "ux_regulacao_analise_espelho_sistema_espelho",
                schema: "smsmarica",
                table: "regulacao_analise_espelho",
                columns: new[] { "sistema", "espelho_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_regulacao_procedimento_origem_esussg_catalogo_recurso_esuss~",
                schema: "smsmarica",
                table: "regulacao_procedimento_origem",
                column: "esussg_catalogo_recurso_id",
                principalSchema: "smsmarica",
                principalTable: "esussg_catalogo_recurso",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_regulacao_procedimento_origem_esussg_catalogo_recurso_esuss~",
                schema: "smsmarica",
                table: "regulacao_procedimento_origem");

            migrationBuilder.DropTable(
                name: "esussg_catalogo_recurso",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "esussg_evento",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "esussg_gatilho",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "esussg_varredura_falha",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "regulacao_analise_espelho",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "esussg_solicitacao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "esussg_varredura_execucao",
                schema: "smsmarica");

            migrationBuilder.DropIndex(
                name: "ux_regulacao_solicitacao_espelho_esussg",
                schema: "smsmarica",
                table: "regulacao_solicitacao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_regulacao_solicitacao_um_espelho",
                schema: "smsmarica",
                table: "regulacao_solicitacao");

            migrationBuilder.DropIndex(
                name: "IX_regulacao_procedimento_origem_esussg_catalogo_recurso_id",
                schema: "smsmarica",
                table: "regulacao_procedimento_origem");

            migrationBuilder.DropColumn(
                name: "esussg_solicitacao_id",
                schema: "smsmarica",
                table: "regulacao_solicitacao");

            migrationBuilder.DropColumn(
                name: "esussg_catalogo_recurso_id",
                schema: "smsmarica",
                table: "regulacao_procedimento_origem");

            migrationBuilder.AddCheckConstraint(
                name: "ck_regulacao_solicitacao_um_espelho",
                schema: "smsmarica",
                table: "regulacao_solicitacao",
                sql: "((solicitacao_id IS NOT NULL)::int + (ser_solicitacao_id IS NOT NULL)::int + (sernit_solicitacao_id IS NOT NULL)::int) <= 1");
        }
    }
}
