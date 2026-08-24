using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <summary>
    /// O procedimento do SISREG passa a ser o fiel: nome (MAIÚSCULAS) e código dele mandam na
    /// lista de solicitações, no exame e no laudo. O SIGTAP vira correlação de faturamento,
    /// opcional.
    ///
    /// <para><b>O que estava errado</b> (medido na produção em 10/08/2026): o tipo de exame era
    /// ancorado no SIGTAP, então procedimentos distintos que compartilham código apareciam com o
    /// mesmo nome. "ULTRASSOM DE ARTICULAÇÃO" (0205020062) exibia 15 procedimentos diferentes
    /// (joelho D/E, ombro D/E, punho D/E, mão D/E, tornozelo D/E, antebraço, panturrilha, perna,
    /// região inguinal); "Ultrassom de abdome total" (0205020143) exibia USG MORFOLÓGICO, USG
    /// OBSTÉTRICA e TRANSLUCÊNCIA NUCAL; "Ultrassom de tireoide" exibia
    /// "ULTRA-SONOGRAFIA TRANSFONTANELAR - INFANTIL". Além disso, 621 solicitações estavam com
    /// <c>procedimento_texto</c> VAZIO, com o nome disponível o tempo todo dentro do RAW.</para>
    ///
    /// <para><b>Backfill:</b> tudo é reconstruído do <c>raw_sisreg</c>, que é a linha crua do
    /// export e guarda o código do procedimento na coluna 1 e o nome na coluna 3.</para>
    ///
    /// <para><b>Não quebra o que já roda:</b> cada tipo novo herda modalidade, unidade padrão e o
    /// gatilho de worklist do tipo que atendia aquele procedimento até agora. Só quem não tem de
    /// quem herdar nasce com modalidade indefinida e worklist desligada — a pendência de
    /// configuração DICOM.</para>
    /// </summary>
    public partial class ProcedimentoSisregEhOFiel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL cru e idempotente nas duas sentenças abaixo, em vez dos helpers do EF, porque a
            // produção JÁ TEM as duas mudanças: a migration EixoProcedimentoSisreg (06/08/2026) foi
            // aplicada lá, mas seu código ficou num stash e nunca entrou no repositório — ela não
            // existe no histórico daqui, só no __migrations do banco. Com AddColumn, esta migração
            // quebraria em produção ("column already exists") e passaria na bancada, que é o pior
            // dos mundos. DROP NOT NULL numa coluna já nullable é no-op; o ADD COLUMN leva
            // IF NOT EXISTS. Nos dois casos vale para banco novo e para a produção como está.
            migrationBuilder.Sql(@"
                ALTER TABLE smsmarica.tipo_exame
                    ALTER COLUMN procedimento_sigtap_id DROP NOT NULL;");

            migrationBuilder.Sql(@"
                ALTER TABLE smsmarica.solicitacao
                    ADD COLUMN IF NOT EXISTS procedimento_codigo_sisreg character varying(20);");

            migrationBuilder.AddColumn<bool>(
                name: "auto_criado",
                schema: "smsmarica",
                table: "tipo_exame",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "codigo_sisreg",
                schema: "smsmarica",
                table: "tipo_exame",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tipo_exame_codigo_sisreg",
                schema: "smsmarica",
                table: "tipo_exame",
                column: "codigo_sisreg",
                filter: "codigo_sisreg IS NOT NULL");

            // ================= BACKFILL =================
            // Só linhas de export de verdade: 38 campos separados por ';'. O que não encaixa
            // (solicitação criada à mão, RAW de formato antigo) fica exatamente como está — não há
            // dado do SISREG para reconstruir, e inventar seria o erro que esta migração corrige.
            const string RawUtilizavel = @"
                raw_sisreg IS NOT NULL
                AND raw_sisreg NOT LIKE '{%'
                AND array_length(string_to_array(raw_sisreg, ';'), 1) >= 38";

            // 1. Nome e código do procedimento, direto do RAW. O nome vai para a forma canônica
            //    (MAIÚSCULAS, espaços colapsados) — a mesma que o ResolvedorTipoExameSisreg aplica,
            //    senão "CONSULTA  EM CARDIOLOGIA" (espaço duplo, como o SISREG manda) viraria um
            //    procedimento diferente de "CONSULTA EM CARDIOLOGIA".
            migrationBuilder.Sql($@"
                UPDATE smsmarica.solicitacao
                   SET procedimento_codigo_sisreg =
                           NULLIF(regexp_replace(split_part(raw_sisreg, ';', 2), '\D', '', 'g'), ''),
                       procedimento_texto = COALESCE(
                           NULLIF(upper(btrim(regexp_replace(split_part(raw_sisreg, ';', 4), '\s+', ' ', 'g'))), ''),
                           procedimento_texto)
                 WHERE {RawUtilizavel};");

            // Solicitações sem RAW aproveitável ainda podem ter texto fora da forma canônica.
            migrationBuilder.Sql(@"
                UPDATE smsmarica.solicitacao
                   SET procedimento_texto = upper(btrim(regexp_replace(procedimento_texto, '\s+', ' ', 'g')))
                 WHERE procedimento_texto IS NOT NULL
                   AND procedimento_texto <> upper(btrim(regexp_replace(procedimento_texto, '\s+', ' ', 'g')));");

            // 2. De quem cada procedimento herda a configuração DICOM: o tipo que hoje atende a
            //    maioria dos exames daquele nome. Guardado ANTES de religar, porque depois de
            //    religar não há mais como saber quem atendia o quê.
            migrationBuilder.Sql(@"
                CREATE TEMP TABLE _origem_tipo_exame ON COMMIT DROP AS
                SELECT DISTINCT ON (nome) nome, tipo_exame_id
                  FROM (SELECT upper(btrim(s.procedimento_texto)) AS nome,
                               ei.tipo_exame_id,
                               count(*) AS qtd
                          FROM smsmarica.exame_imagem ei
                          JOIN smsmarica.solicitacao s ON s.id = ei.solicitacao_id
                         WHERE ei.excluido_em IS NULL
                           AND s.excluido_em IS NULL
                           AND ei.tipo_exame_id IS NOT NULL
                           AND COALESCE(btrim(s.procedimento_texto), '') <> ''
                         GROUP BY 1, 2) x
                 ORDER BY nome, qtd DESC, tipo_exame_id;");

            // 3. Um tipo de exame por procedimento do SISREG que tem exame de imagem.
            //    As descrições DICOM passam a ser o nome do SISREG: é ele que deve chegar ao
            //    equipamento, não um rótulo que alguém escolheu aqui dentro.
            //
            //    procedimento_sigtap_id fica NULL DE PROPÓSITO. O código que o SISREG exporta como
            //    "SIGTAP" vem de uma versão defasada da tabela e, consultado no SIGTAP oficial,
            //    devolve OUTRO procedimento — medido em 10/08/2026: o 0205020127 que o SISREG usa
            //    para "ULTRASONOGRAFIA DE TIREOIDE" é "ULTRASSONOGRAFIA PÉLVICA (GINECOLÓGICA)" no
            //    oficial, e o 0205020178 de "TRANSFONTANELAR - INFANTIL" é "DE TIREOIDE". Gravar
            //    essa ligação seria afirmar uma equivalência falsa — que é a origem do exame de
            //    dois recém-nascidos ter ido para o aparelho rotulado como tireoide. A correlação
            //    para faturamento é trabalho à parte, com a tabela oficial na mão.
            migrationBuilder.Sql(@"
                INSERT INTO smsmarica.tipo_exame (
                    id, nome, codigo_sisreg, auto_criado, procedimento_sigtap_id, modalidade_dicom,
                    requested_procedure_description, scheduled_procedure_step_description,
                    codigos_protocolo, tempo_estimado_minutos, unidade_padrao_id, ativo,
                    enviar_para_worklist, criado_em)
                SELECT gen_random_uuid(), o.nome, NULL, TRUE, NULL,
                       COALESCE(origem.modalidade_dicom, 0),
                       left(o.nome, 200), left(o.nome, 200),
                       '{}'::text[], origem.tempo_estimado_minutos, origem.unidade_padrao_id, TRUE,
                       COALESCE(origem.enviar_para_worklist, FALSE), now() AT TIME ZONE 'utc'
                  FROM _origem_tipo_exame o
                  LEFT JOIN smsmarica.tipo_exame origem ON origem.id = o.tipo_exame_id
                 WHERE NOT EXISTS (SELECT 1 FROM smsmarica.tipo_exame t
                                    WHERE t.nome = o.nome AND t.excluido_em IS NULL);");

            // 4. Religa os exames pelo NOME do procedimento.
            migrationBuilder.Sql(@"
                UPDATE smsmarica.exame_imagem ei
                   SET tipo_exame_id = t.id,
                       atualizado_em = now() AT TIME ZONE 'utc'
                  FROM smsmarica.solicitacao s, smsmarica.tipo_exame t
                 WHERE s.id = ei.solicitacao_id
                   AND ei.excluido_em IS NULL
                   AND s.excluido_em IS NULL
                   AND t.excluido_em IS NULL
                   AND t.nome = upper(btrim(s.procedimento_texto))
                   AND ei.tipo_exame_id IS DISTINCT FROM t.id;");

            // 5. O código do SISREG, quando alguma linha daquele procedimento o trouxe. Vem vazio
            //    em ~1/3 das linhas, então basta UMA tê-lo para o tipo inteiro ficar com o código.
            migrationBuilder.Sql($@"
                WITH pa AS (
                    SELECT DISTINCT ON (nome) nome, codigo
                      FROM (SELECT upper(btrim(procedimento_texto)) AS nome,
                                   NULLIF(regexp_replace(split_part(raw_sisreg, ';', 2), '\D', '', 'g'), '') AS codigo,
                                   count(*) AS qtd
                              FROM smsmarica.solicitacao
                             WHERE excluido_em IS NULL
                               AND COALESCE(btrim(procedimento_texto), '') <> ''
                               AND {RawUtilizavel}
                             GROUP BY 1, 2) x
                     WHERE codigo IS NOT NULL
                     ORDER BY nome, qtd DESC)
                UPDATE smsmarica.tipo_exame t
                   SET codigo_sisreg = pa.codigo
                  FROM pa
                 WHERE t.nome = pa.nome
                   AND t.codigo_sisreg IS NULL
                   AND t.excluido_em IS NULL;");

            // 6. Os tipos antigos que perderam TODOS os exames para os novos saem do formulário de
            //    solicitação — senão a equipe veria o rótulo velho e o nome do SISREG lado a lado,
            //    escolhendo entre duas versões do mesmo procedimento. Só desativa (nunca exclui) e
            //    só quem de fato era origem de alguém: tipo cadastrado à mão que ainda não foi
            //    usado continua disponível.
            migrationBuilder.Sql(@"
                UPDATE smsmarica.tipo_exame t
                   SET ativo = FALSE,
                       atualizado_em = now() AT TIME ZONE 'utc'
                 WHERE t.excluido_em IS NULL
                   AND t.ativo
                   AND t.id IN (SELECT tipo_exame_id FROM _origem_tipo_exame WHERE tipo_exame_id IS NOT NULL)
                   AND NOT EXISTS (SELECT 1 FROM smsmarica.exame_imagem ei
                                    WHERE ei.tipo_exame_id = t.id AND ei.excluido_em IS NULL);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tipo_exame_codigo_sisreg",
                schema: "smsmarica",
                table: "tipo_exame");

            migrationBuilder.DropColumn(
                name: "auto_criado",
                schema: "smsmarica",
                table: "tipo_exame");

            migrationBuilder.DropColumn(
                name: "codigo_sisreg",
                schema: "smsmarica",
                table: "tipo_exame");

            migrationBuilder.DropColumn(
                name: "procedimento_codigo_sisreg",
                schema: "smsmarica",
                table: "solicitacao");

            migrationBuilder.AlterColumn<Guid>(
                name: "procedimento_sigtap_id",
                schema: "smsmarica",
                table: "tipo_exame",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
