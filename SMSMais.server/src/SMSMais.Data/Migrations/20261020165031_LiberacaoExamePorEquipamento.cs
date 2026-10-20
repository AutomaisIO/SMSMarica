using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class LiberacaoExamePorEquipamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "liberado_paciente_em",
                schema: "smsmarica",
                table: "exame_imagem",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "liberado_paciente_por",
                schema: "smsmarica",
                table: "exame_imagem",
                type: "uuid",
                nullable: true);

            // Os aparelhos que já existem nascem no lado seguro (ADR-0075): laudo na plataforma
            // (1), exame retido até o laudo assinado (1), com anamnese. O default fica só aqui —
            // ver EquipamentoConfiguration: no modelo ele faria o EF omitir o false do INSERT.
            migrationBuilder.AddColumn<int>(
                name: "liberacao_paciente",
                schema: "smsmarica",
                table: "equipamento",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "origem_laudo",
                schema: "smsmarica",
                table: "equipamento",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "usa_anamnese",
                schema: "smsmarica",
                table: "equipamento",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Ultrassom: laudado fora da plataforma e liberado ao chegar. Medido em 20/10/2026:
            // nenhum dos 1.359 exames de US realizados teve laudo aqui — o médico lauda na hora, em
            // outra plataforma. Instância nova não tem aparelho quando a migration roda: não faz nada.
            migrationBuilder.Sql(
                "UPDATE smsmarica.equipamento SET origem_laudo = 3, liberacao_paciente = 2 WHERE modalidade_dicom = 4;");

            // ---- Liberação dos exames que já foram realizados ----
            // Até aqui todo exame realizado aparecia no app do cidadão assim que chegava. Agora só
            // o liberado: os passos abaixo gravam a liberação de quem já devia estar liberado, e o
            // que sobra fica RETIDO até o laudo (decisão de 20/10/2026: reter também o que ainda
            // não tinha sido avisado).

            // 1) O paciente já recebeu o "Exame liberado": continua liberado. Tirar do app o que
            //    foi mandado por WhatsApp só deixaria o link quebrado na mão dele.
            migrationBuilder.Sql("""
                UPDATE smsmarica.exame_imagem ei
                SET liberado_paciente_em = c.enviado_em
                FROM smsmarica.comunicacao_paciente c
                WHERE c.solicitacao_id = ei.solicitacao_id
                  AND c.finalidade = 2 AND c.enviado_em IS NOT NULL
                  AND ei.status IN (4, 5) AND ei.liberado_paciente_em IS NULL;
                """);

            // 2) Laudo assinado: liberado na aprovação. O study do exame é o dele ou o da associação
            //    (exame sem worklist, ex.: a mamografia do Fuji) — a mesma regra do app do cidadão.
            migrationBuilder.Sql("""
                UPDATE smsmarica.exame_imagem ei
                SET liberado_paciente_em = x.aprovado_em
                FROM (
                    SELECT st.exame_id,
                           min(coalesce(la.atualizado_em, la.assinado_em, la.criado_em)) AS aprovado_em
                    FROM (
                        SELECT e.id AS exame_id, e.study_instance_uid AS uid
                        FROM smsmarica.exame_imagem e WHERE e.study_instance_uid <> ''
                        UNION
                        SELECT a.exame_imagem_id, a.study_instance_uid
                        FROM smsmarica.exame_associacao a
                        WHERE a.excluido_em IS NULL AND a.study_instance_uid <> ''
                    ) st
                    JOIN smsmarica.laudo l ON l.study_instance_uid = st.uid AND NOT l.excluido
                    JOIN smsmarica.laudo_assinatura la ON la.laudo_id = l.id AND la.status = 2
                    GROUP BY st.exame_id
                ) x
                WHERE ei.id = x.exame_id AND ei.status IN (4, 5) AND ei.liberado_paciente_em IS NULL;
                """);

            // 3) Aparelho que libera ao chegar: liberado na chegada. A dedução do aparelho é a do
            //    RegrasDoExameService — o do exame, o fixo do escopo, ou os da unidade na modalidade
            //    (vários: só se todos liberam ao chegar).
            migrationBuilder.Sql("""
                WITH ex AS (
                    SELECT ei.id, ei.equipamento_id, ei.tipo_exame_id, t.modalidade_dicom AS modalidade,
                           s.unidade_executante_id AS unidade_id,
                           coalesce(ei.realizado_em, ei.criado_em) AS chegou_em
                    FROM smsmarica.exame_imagem ei
                    JOIN smsmarica.solicitacao s ON s.id = ei.solicitacao_id
                    LEFT JOIN smsmarica.tipo_exame t ON t.id = ei.tipo_exame_id
                    WHERE ei.status IN (4, 5) AND ei.liberado_paciente_em IS NULL
                ),
                fixo AS (
                    SELECT DISTINCT ON (teu.tipo_exame_id, teu.unidade_id)
                           teu.tipo_exame_id, teu.unidade_id, teu.equipamento_id
                    FROM smsmarica.tipo_exame_unidade teu
                    WHERE teu.excluido_em IS NULL AND teu.equipamento_id IS NOT NULL
                    ORDER BY teu.tipo_exame_id, teu.unidade_id, teu.ativo DESC
                ),
                candidatos AS (
                    SELECT e.unidade_id, e.modalidade_dicom, bool_and(e.liberacao_paciente = 2) AS todos_ao_chegar
                    FROM smsmarica.equipamento e
                    WHERE e.ativo AND e.excluido_em IS NULL
                    GROUP BY e.unidade_id, e.modalidade_dicom
                ),
                regra AS (
                    SELECT ex.id, ex.chegou_em,
                           CASE
                               WHEN ex.equipamento_id IS NOT NULL THEN eq.liberacao_paciente = 2
                               WHEN f.equipamento_id IS NOT NULL THEN ef.liberacao_paciente = 2
                               ELSE coalesce(c.todos_ao_chegar, false)
                           END AS ao_chegar
                    FROM ex
                    LEFT JOIN smsmarica.equipamento eq ON eq.id = ex.equipamento_id
                    LEFT JOIN fixo f ON f.tipo_exame_id = ex.tipo_exame_id AND f.unidade_id = ex.unidade_id
                    LEFT JOIN smsmarica.equipamento ef ON ef.id = f.equipamento_id
                    LEFT JOIN candidatos c ON c.unidade_id = ex.unidade_id AND c.modalidade_dicom = ex.modalidade
                )
                UPDATE smsmarica.exame_imagem ei
                SET liberado_paciente_em = r.chegou_em
                FROM regra r
                WHERE ei.id = r.id AND r.ao_chegar;
                """);

            // 4) Uma mensagem só: o "Exame liberado" automático que ainda espera sair (a maioria à
            //    espera do telefone verificado) numa solicitação que já tem o "Laudo pronto" fica
            //    coberto por ele — o link do laudo abre o exame com imagem e laudo. Daqui para a
            //    frente quem faz isso é a aprovação do laudo (LaudoAssinaturaService).
            migrationBuilder.Sql("""
                UPDATE smsmarica.comunicacao_paciente c
                SET status = 11,
                    motivo_falha = 'Coberto pelo "Laudo pronto": imagem e laudo vão juntos numa mensagem só.',
                    proxima_tentativa_em = NULL,
                    atualizado_em = now()
                WHERE c.finalidade = 2 AND c.origem = 0 AND c.enviado_em IS NULL
                  AND c.status IN (1, 7, 8, 9)
                  AND EXISTS (SELECT 1 FROM smsmarica.comunicacao_paciente lp
                              WHERE lp.solicitacao_id = c.solicitacao_id AND lp.finalidade = 3);
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_equipamento_liberacao_com_laudo",
                schema: "smsmarica",
                table: "equipamento",
                sql: "liberacao_paciente <> 1 OR origem_laudo = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_equipamento_liberacao_com_laudo",
                schema: "smsmarica",
                table: "equipamento");

            migrationBuilder.DropColumn(
                name: "liberado_paciente_em",
                schema: "smsmarica",
                table: "exame_imagem");

            migrationBuilder.DropColumn(
                name: "liberado_paciente_por",
                schema: "smsmarica",
                table: "exame_imagem");

            migrationBuilder.DropColumn(
                name: "liberacao_paciente",
                schema: "smsmarica",
                table: "equipamento");

            migrationBuilder.DropColumn(
                name: "origem_laudo",
                schema: "smsmarica",
                table: "equipamento");

            migrationBuilder.DropColumn(
                name: "usa_anamnese",
                schema: "smsmarica",
                table: "equipamento");
        }
    }
}
