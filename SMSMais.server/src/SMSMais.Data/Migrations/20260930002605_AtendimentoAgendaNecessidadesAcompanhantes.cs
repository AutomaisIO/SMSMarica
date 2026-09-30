using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <summary>
    /// Atendimento do Transporte de Pacientes: agenda por dias da semana (N sessões ou contínuo),
    /// condição do paciente para a viagem, acompanhantes do paciente e tempo médio no tipo.
    ///
    /// <para><b>ESCRITA À MÃO.</b> O scaffold do EF tomou <c>tratamento.tempo_medio_minutos</c> por um
    /// RENAME para <c>quantidade_sessoes</c> (seria gravar minutos como número de sessões) e emitiu um
    /// UpdateData que zeraria o tempo do tipo. Nada disso aqui. Ordem: o tempo médio dos atendimentos
    /// vai para o tipo que ainda não tem; a agenda nova nasce das sessões e da periodicidade antiga;
    /// só depois as colunas e a tabela antigas saem.</para>
    /// </summary>
    public partial class AtendimentoAgendaNecessidadesAcompanhantes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Tempo médio passa a ser do tipo. O que estava nos atendimentos vai para o tipo que
            //    ainda não tem valor (o maior, se houver mais de um) — nada se perde.
            migrationBuilder.AddColumn<int>(
                name: "tempo_medio_minutos",
                schema: "smsmarica",
                table: "tipo_tratamento",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE smsmarica.tipo_tratamento tt
                   SET tempo_medio_minutos = x.tempo
                  FROM (SELECT tipo_tratamento_id, max(tempo_medio_minutos) AS tempo
                          FROM smsmarica.tratamento
                         WHERE tipo_tratamento_id IS NOT NULL AND tempo_medio_minutos IS NOT NULL
                         GROUP BY tipo_tratamento_id) x
                 WHERE tt.id = x.tipo_tratamento_id
                   AND tt.tempo_medio_minutos IS NULL;
                """);

            // 2) Agenda nova. data_inicio e dias_semana_mascara nascem anuláveis para o backfill.
            migrationBuilder.AddColumn<DateOnly>(
                name: "data_inicio", schema: "smsmarica", table: "tratamento", type: "date", nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "dias_semana_mascara", schema: "smsmarica", table: "tratamento", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "quantidade_sessoes", schema: "smsmarica", table: "tratamento", type: "integer", nullable: true);
            migrationBuilder.AddColumn<bool>(
                name: "continuo", schema: "smsmarica", table: "tratamento", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<DateOnly>(
                name: "sessoes_geradas_ate", schema: "smsmarica", table: "tratamento", type: "date", nullable: true);

            // Sessões são a verdade do que foi agendado: início = primeira, dias = os dias da semana
            // em que houve sessão (bit 0 = domingo, igual ao extract(dow)), total = as não canceladas.
            // Sem sessão nenhuma, cai na periodicidade antiga e, por fim, no dia do cadastro.
            migrationBuilder.Sql("""
                WITH s AS (
                    SELECT tratamento_id,
                           min(data_prevista) AS primeira,
                           max(data_prevista) AS ultima,
                           count(*) FILTER (WHERE status <> 4) AS quantidade,
                           bit_or(1 << extract(dow FROM data_prevista)::int) AS mascara
                      FROM smsmarica.sessao_de_tratamento
                     GROUP BY tratamento_id
                ),
                base AS (
                    SELECT t.id,
                           COALESCE(s.primeira, p.data_inicio, (t.criado_em AT TIME ZONE 'America/Sao_Paulo')::date) AS inicio,
                           s.ultima,
                           s.quantidade,
                           s.mascara,
                           p.dias_semana_mascara AS mascara_antiga,
                           p.quantidade_sessoes AS quantidade_antiga
                      FROM smsmarica.tratamento t
                      LEFT JOIN s ON s.tratamento_id = t.id
                      LEFT JOIN smsmarica.tratamento_periodicidade p ON p.tratamento_id = t.id
                )
                UPDATE smsmarica.tratamento t
                   SET data_inicio = b.inicio,
                       dias_semana_mascara = COALESCE(b.mascara, NULLIF(b.mascara_antiga, 0), 1 << extract(dow FROM b.inicio)::int),
                       quantidade_sessoes = LEAST(GREATEST(COALESCE(NULLIF(b.quantidade, 0), b.quantidade_antiga, 1), 1), 365),
                       sessoes_geradas_ate = b.ultima
                  FROM base b
                 WHERE t.id = b.id;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE smsmarica.tratamento ALTER COLUMN data_inicio SET NOT NULL;
                ALTER TABLE smsmarica.tratamento ALTER COLUMN dias_semana_mascara SET NOT NULL;
                """);

            // 3) Condição do paciente para a viagem e regra de acompanhantes.
            migrationBuilder.AddColumn<int>(
                name: "mobilidade", schema: "smsmarica", table: "tratamento", type: "integer", nullable: false, defaultValue: 1);
            migrationBuilder.AddColumn<bool>(
                name: "dificuldade_veiculo_alto", schema: "smsmarica", table: "tratamento", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(
                name: "isolamento", schema: "smsmarica", table: "tratamento", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(
                name: "usa_oxigenio", schema: "smsmarica", table: "tratamento", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(
                name: "necessita_ajuda", schema: "smsmarica", table: "tratamento", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>(
                name: "ajuda_descricao", schema: "smsmarica", table: "tratamento", type: "character varying(500)", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "quantidade_acompanhantes", schema: "smsmarica", table: "tratamento", type: "integer", nullable: false, defaultValue: 1);
            migrationBuilder.AddColumn<string>(
                name: "segundo_acompanhante_justificativa", schema: "smsmarica", table: "tratamento", type: "character varying(500)", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<Guid>(
                name: "segundo_acompanhante_liberado_por", schema: "smsmarica", table: "tratamento", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<DateTime>(
                name: "segundo_acompanhante_liberado_em", schema: "smsmarica", table: "tratamento", type: "timestamp with time zone", nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_tratamento_agenda",
                schema: "smsmarica",
                table: "tratamento",
                sql: "(continuo AND quantidade_sessoes IS NULL) OR (NOT continuo AND quantidade_sessoes BETWEEN 1 AND 365)");
            migrationBuilder.AddCheckConstraint(
                name: "ck_tratamento_dias_semana",
                schema: "smsmarica",
                table: "tratamento",
                sql: "dias_semana_mascara BETWEEN 1 AND 127");
            migrationBuilder.AddCheckConstraint(
                name: "ck_tratamento_quantidade_acompanhantes",
                schema: "smsmarica",
                table: "tratamento",
                sql: "quantidade_acompanhantes BETWEEN 1 AND 2");

            // 4) Só agora sai o que foi substituído (os dados já foram aproveitados acima).
            migrationBuilder.DropColumn(name: "codigo_sus_liberacao", schema: "smsmarica", table: "tratamento");
            migrationBuilder.DropColumn(name: "hora_prevista_busca", schema: "smsmarica", table: "tratamento");
            migrationBuilder.DropColumn(name: "tempo_medio_minutos", schema: "smsmarica", table: "tratamento");
            migrationBuilder.DropTable(name: "tratamento_periodicidade", schema: "smsmarica");

            // 5) Acompanhantes do paciente e quem vai em cada viagem.
            migrationBuilder.CreateTable(
                name: "acompanhante",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cpf = table.Column<string>(type: "character(11)", fixedLength: true, maxLength: 11, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    data_nascimento = table.Column<DateOnly>(type: "date", nullable: false),
                    parentesco = table.Column<int>(type: "integer", nullable: true),
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    paciente_vinculado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fonte_nome = table.Column<int>(type: "integer", nullable: false),
                    origem = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_acompanhante", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sessao_acompanhante",
                schema: "smsmarica",
                columns: table => new
                {
                    sessao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    acompanhante_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessao_acompanhante", x => new { x.sessao_id, x.acompanhante_id });
                    table.ForeignKey(
                        name: "FK_sessao_acompanhante_acompanhante_acompanhante_id",
                        column: x => x.acompanhante_id,
                        principalSchema: "smsmarica",
                        principalTable: "acompanhante",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sessao_acompanhante_sessao_de_tratamento_sessao_id",
                        column: x => x.sessao_id,
                        principalSchema: "smsmarica",
                        principalTable: "sessao_de_tratamento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_acompanhante_paciente_id_cpf",
                schema: "smsmarica",
                table: "acompanhante",
                columns: new[] { "paciente_id", "cpf" },
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_sessao_acompanhante_acompanhante_id",
                schema: "smsmarica",
                table: "sessao_acompanhante",
                column: "acompanhante_id");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Volta a estrutura antiga. Acompanhantes e condição do paciente se perdem; a periodicidade
        /// volta como "dias fixos da semana" com o total de sessões, e o tempo médio volta para o
        /// atendimento a partir do tipo. Código SUS e horário padrão não voltam (já tinham saído).
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "sessao_acompanhante", schema: "smsmarica");
            migrationBuilder.DropTable(name: "acompanhante", schema: "smsmarica");

            migrationBuilder.AddColumn<string>(
                name: "codigo_sus_liberacao", schema: "smsmarica", table: "tratamento", type: "character varying(60)", maxLength: 60, nullable: true);
            migrationBuilder.AddColumn<TimeOnly>(
                name: "hora_prevista_busca", schema: "smsmarica", table: "tratamento", type: "time without time zone", nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "tempo_medio_minutos", schema: "smsmarica", table: "tratamento", type: "integer", nullable: true);

            migrationBuilder.CreateTable(
                name: "tratamento_periodicidade",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tratamento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    dias_semana_mascara = table.Column<int>(type: "integer", nullable: true),
                    intervalo_dias = table.Column<int>(type: "integer", nullable: true),
                    quantidade_sessoes = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tratamento_periodicidade", x => x.id);
                    table.ForeignKey(
                        name: "FK_tratamento_periodicidade_tratamento_tratamento_id",
                        column: x => x.tratamento_id,
                        principalSchema: "smsmarica",
                        principalTable: "tratamento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tratamento_periodicidade_tratamento_id",
                schema: "smsmarica",
                table: "tratamento_periodicidade",
                column: "tratamento_id",
                unique: true);

            // tipo 3 = SemanaDiasFixos (enum antigo TipoPeriodicidade).
            migrationBuilder.Sql("""
                INSERT INTO smsmarica.tratamento_periodicidade
                       (id, tratamento_id, criado_em, data_inicio, dias_semana_mascara, intervalo_dias, quantidade_sessoes, tipo)
                SELECT gen_random_uuid(), t.id, t.criado_em, t.data_inicio, t.dias_semana_mascara, NULL,
                       COALESCE(t.quantidade_sessoes, (SELECT count(*) FROM smsmarica.sessao_de_tratamento s WHERE s.tratamento_id = t.id)), 3
                  FROM smsmarica.tratamento t;

                UPDATE smsmarica.tratamento t
                   SET tempo_medio_minutos = tt.tempo_medio_minutos
                  FROM smsmarica.tipo_tratamento tt
                 WHERE tt.id = t.tipo_tratamento_id;
                """);

            migrationBuilder.DropCheckConstraint(name: "ck_tratamento_agenda", schema: "smsmarica", table: "tratamento");
            migrationBuilder.DropCheckConstraint(name: "ck_tratamento_dias_semana", schema: "smsmarica", table: "tratamento");
            migrationBuilder.DropCheckConstraint(name: "ck_tratamento_quantidade_acompanhantes", schema: "smsmarica", table: "tratamento");

            migrationBuilder.DropColumn(name: "ajuda_descricao", schema: "smsmarica", table: "tratamento");
            migrationBuilder.DropColumn(name: "continuo", schema: "smsmarica", table: "tratamento");
            migrationBuilder.DropColumn(name: "data_inicio", schema: "smsmarica", table: "tratamento");
            migrationBuilder.DropColumn(name: "dias_semana_mascara", schema: "smsmarica", table: "tratamento");
            migrationBuilder.DropColumn(name: "dificuldade_veiculo_alto", schema: "smsmarica", table: "tratamento");
            migrationBuilder.DropColumn(name: "isolamento", schema: "smsmarica", table: "tratamento");
            migrationBuilder.DropColumn(name: "mobilidade", schema: "smsmarica", table: "tratamento");
            migrationBuilder.DropColumn(name: "necessita_ajuda", schema: "smsmarica", table: "tratamento");
            migrationBuilder.DropColumn(name: "quantidade_acompanhantes", schema: "smsmarica", table: "tratamento");
            migrationBuilder.DropColumn(name: "quantidade_sessoes", schema: "smsmarica", table: "tratamento");
            migrationBuilder.DropColumn(name: "segundo_acompanhante_justificativa", schema: "smsmarica", table: "tratamento");
            migrationBuilder.DropColumn(name: "segundo_acompanhante_liberado_em", schema: "smsmarica", table: "tratamento");
            migrationBuilder.DropColumn(name: "segundo_acompanhante_liberado_por", schema: "smsmarica", table: "tratamento");
            migrationBuilder.DropColumn(name: "sessoes_geradas_ate", schema: "smsmarica", table: "tratamento");
            migrationBuilder.DropColumn(name: "usa_oxigenio", schema: "smsmarica", table: "tratamento");

            migrationBuilder.DropColumn(name: "tempo_medio_minutos", schema: "smsmarica", table: "tipo_tratamento");
        }
    }
}
