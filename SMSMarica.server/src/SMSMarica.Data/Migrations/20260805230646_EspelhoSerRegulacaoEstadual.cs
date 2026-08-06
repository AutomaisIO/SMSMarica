using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class EspelhoSerRegulacaoEstadual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ser_solicitacao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_ser = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    recurso = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    data_solicitacao = table.Column<DateOnly>(type: "date", nullable: true),
                    paciente_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    idade_texto = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    cns = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    cid = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    solicitante_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    municipio_solicitante = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    agendado_para_texto = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    nome_mae = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sexo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    data_nascimento = table.Column<DateOnly>(type: "date", nullable: true),
                    etnia = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    cep = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    municipio_paciente = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    bairro = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    tipo_logradouro = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    logradouro = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    numero = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    complemento = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    telefone_residencial = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    telefone_whatsapp = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    telefone_contato = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    sincronizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    historico_lido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    eventos_count = table.Column<int>(type: "integer", nullable: false),
                    ultimo_evento_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    historico_indisponivel = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_ser_solicitacao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ser_varredura_execucao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    modo = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    disparo = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    status = table.Column<int>(type: "integer", nullable: false),
                    janela_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    janela_fim = table.Column<DateOnly>(type: "date", nullable: false),
                    situacoes_varridas = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    buscas = table.Column<int>(type: "integer", nullable: false),
                    paginas = table.Column<int>(type: "integer", nullable: false),
                    solicitacoes_encontradas = table.Column<int>(type: "integer", nullable: false),
                    solicitacoes_novas = table.Column<int>(type: "integer", nullable: false),
                    solicitacoes_atualizadas = table.Column<int>(type: "integer", nullable: false),
                    mudancas_situacao = table.Column<int>(type: "integer", nullable: false),
                    historicos_lidos = table.Column<int>(type: "integer", nullable: false),
                    eventos_novos = table.Column<int>(type: "integer", nullable: false),
                    followups_novos = table.Column<int>(type: "integer", nullable: false),
                    historicos_indisponiveis = table.Column<int>(type: "integer", nullable: false),
                    gatilhos_gerados = table.Column<int>(type: "integer", nullable: false),
                    fatias_truncadas = table.Column<int>(type: "integer", nullable: false),
                    mensagem_erro = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    iniciado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finalizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    duracao_segundos = table.Column<int>(type: "integer", nullable: true),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    criado_por_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ser_varredura_execucao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ser_evento",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ser_solicitacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_evento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    evento = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    estado_anterior = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    estado_atual = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    central_regulacao = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    unidade_executora = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    usuario = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    lotacao_evento = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    observacao = table.Column<string>(type: "text", nullable: true),
                    capturado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ser_evento", x => x.id);
                    table.ForeignKey(
                        name: "FK_ser_evento_ser_solicitacao_ser_solicitacao_id",
                        column: x => x.ser_solicitacao_id,
                        principalSchema: "smsmarica",
                        principalTable: "ser_solicitacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ser_gatilho",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ser_solicitacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_ser = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_ser_gatilho", x => x.id);
                    table.ForeignKey(
                        name: "FK_ser_gatilho_ser_solicitacao_ser_solicitacao_id",
                        column: x => x.ser_solicitacao_id,
                        principalSchema: "smsmarica",
                        principalTable: "ser_solicitacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ser_varredura_falha",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    execucao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    situacao = table.Column<int>(type: "integer", nullable: true),
                    fatia_inicio = table.Column<DateOnly>(type: "date", nullable: true),
                    fatia_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    tipo_recurso = table.Column<int>(type: "integer", nullable: true),
                    id_ser = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    mensagem = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    detalhe = table.Column<string>(type: "text", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ser_varredura_falha", x => x.id);
                    table.ForeignKey(
                        name: "FK_ser_varredura_falha_ser_varredura_execucao_execucao_id",
                        column: x => x.execucao_id,
                        principalSchema: "smsmarica",
                        principalTable: "ser_varredura_execucao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ser_evento_evento_capturado",
                schema: "smsmarica",
                table: "ser_evento",
                columns: new[] { "evento", "capturado_em" });

            migrationBuilder.CreateIndex(
                name: "ix_ser_evento_solicitacao_data",
                schema: "smsmarica",
                table: "ser_evento",
                columns: new[] { "ser_solicitacao_id", "data_evento" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ux_ser_evento_solicitacao_data_evento",
                schema: "smsmarica",
                table: "ser_evento",
                columns: new[] { "ser_solicitacao_id", "data_evento", "evento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ser_gatilho_pendente",
                schema: "smsmarica",
                table: "ser_gatilho",
                columns: new[] { "tipo", "criado_em" },
                filter: "processado_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_ser_gatilho_solicitacao_tipo_chave",
                schema: "smsmarica",
                table: "ser_gatilho",
                columns: new[] { "ser_solicitacao_id", "tipo", "chave_evento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ser_solicitacao_cns",
                schema: "smsmarica",
                table: "ser_solicitacao",
                column: "cns");

            migrationBuilder.CreateIndex(
                name: "ix_ser_solicitacao_cpf",
                schema: "smsmarica",
                table: "ser_solicitacao",
                column: "cpf");

            migrationBuilder.CreateIndex(
                name: "ix_ser_solicitacao_situacao_data",
                schema: "smsmarica",
                table: "ser_solicitacao",
                columns: new[] { "situacao", "data_solicitacao" });

            migrationBuilder.CreateIndex(
                name: "ix_ser_solicitacao_situacao_historico_lido",
                schema: "smsmarica",
                table: "ser_solicitacao",
                columns: new[] { "situacao", "historico_lido_em" });

            migrationBuilder.CreateIndex(
                name: "ux_ser_solicitacao_id_ser",
                schema: "smsmarica",
                table: "ser_solicitacao",
                column: "id_ser",
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_ser_varredura_execucao_iniciado",
                schema: "smsmarica",
                table: "ser_varredura_execucao",
                column: "iniciado_em",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_ser_varredura_execucao_status",
                schema: "smsmarica",
                table: "ser_varredura_execucao",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_ser_varredura_falha_execucao_tipo",
                schema: "smsmarica",
                table: "ser_varredura_falha",
                columns: new[] { "execucao_id", "tipo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ser_evento",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ser_gatilho",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ser_varredura_falha",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ser_solicitacao",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "ser_varredura_execucao",
                schema: "smsmarica");
        }
    }
}
