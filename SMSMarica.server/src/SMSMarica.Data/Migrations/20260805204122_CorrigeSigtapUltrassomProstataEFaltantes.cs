using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class CorrigeSigtapUltrassomProstataEFaltantes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "smsmarica",
                table: "procedimento_sigtap",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000022"),
                columns: new[] { "descricao", "nome" },
                values: new object[] { "ULTRASSONOGRAFIA DE PROSTATA (VIA ABDOMINAL)", "ULTRASSONOGRAFIA DE PROSTATA (VIA ABDOMINAL)" });

            migrationBuilder.UpdateData(
                schema: "smsmarica",
                table: "procedimento_sigtap",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000025"),
                column: "codigo",
                value: "02.05.02.009-7");

            migrationBuilder.InsertData(
                schema: "smsmarica",
                table: "procedimento_sigtap",
                columns: new[] { "id", "ativo", "codigo", "competencia_fim", "competencia_inicio", "descricao", "forma", "grupo", "nome", "subgrupo" },
                values: new object[,]
                {
                    { new Guid("a1000000-0000-0000-0000-000000000027"), true, "02.05.02.005-4", null, new DateOnly(2025, 1, 1), "ULTRASSONOGRAFIA DE APARELHO URINARIO", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "ULTRASSONOGRAFIA DE APARELHO URINARIO", "DIAGNOSTICO POR ULTRASSONOGRAFIA" },
                    { new Guid("a1000000-0000-0000-0000-000000000028"), true, "02.05.02.003-8", null, new DateOnly(2025, 1, 1), "ULTRASSONOGRAFIA DE ABDOME SUPERIOR", "EXAMES", "PROCEDIMENTOS COM FINALIDADE DIAGNOSTICA", "ULTRASSONOGRAFIA DE ABDOME SUPERIOR", "DIAGNOSTICO POR ULTRASSONOGRAFIA" }
                });

            // ---- Tipos de exame (dado operacional, não semeado — por isso vai em SQL) ----

            // O tipo ligado a 0205020100 chamava-se "Ultrassom obstétrico" e recebeu 51 exames de
            // PRÓSTATA desde 31/07 — a worklist iria ao aparelho com "US OBSTETRICO". Renomear, em
            // vez de criar outro, preserva o vínculo desses 51: eles já apontam para este tipo e
            // passam a estar corretamente descritos. Conferido antes de mexer: nenhum exame
            // obstétrico legítimo usava este tipo (51 de próstata, 2 sem texto, 0 obstétricos).
            migrationBuilder.Sql("""
                UPDATE smsmarica.tipo_exame SET
                    nome = 'Ultrassom de próstata (via abdominal)',
                    requested_procedure_description = 'US PROSTATA',
                    scheduled_procedure_step_description = 'Ultrassonografia de próstata (via abdominal)',
                    atualizado_em = now()
                WHERE procedimento_sigtap_id = 'a1000000-0000-0000-0000-000000000022';
                """);

            // Tipos para os dois códigos que faltavam. Espelham os demais ultrassons: modalidade
            // US (4), worklist ligada, 20 min.
            migrationBuilder.Sql("""
                INSERT INTO smsmarica.tipo_exame
                    (id, nome, procedimento_sigtap_id, modalidade_dicom,
                     requested_procedure_description, scheduled_procedure_step_description,
                     codigos_protocolo, tempo_estimado_minutos, ativo, criado_em, enviar_para_worklist)
                VALUES
                    ('b2000000-0000-0000-0000-000000000027', 'Ultrassom de aparelho urinário',
                     'a1000000-0000-0000-0000-000000000027', 4,
                     'US APARELHO URINARIO', 'Ultrassonografia de aparelho urinário',
                     '{}', 20, true, now(), true),
                    ('b2000000-0000-0000-0000-000000000028', 'Ultrassom de abdome superior',
                     'a1000000-0000-0000-0000-000000000028', 4,
                     'US ABDOME SUPERIOR', 'Ultrassonografia de abdome superior',
                     '{}', 20, true, now(), true);
                """);

            // Religa os exames que entraram sem tipo. Casamento pelo SIGTAP que o SISREG mandou
            // (gravado na solicitação) — mesma regra da importação.
            migrationBuilder.Sql("""
                UPDATE smsmarica.exame_imagem e
                SET tipo_exame_id = 'b2000000-0000-0000-0000-000000000027'
                FROM smsmarica.solicitacao s
                WHERE s.id = e.solicitacao_id AND e.tipo_exame_id IS NULL
                  AND replace(replace(s.procedimento_sigtap_codigo,'.',''),'-','') = '0205020054';

                UPDATE smsmarica.exame_imagem e
                SET tipo_exame_id = 'b2000000-0000-0000-0000-000000000028'
                FROM smsmarica.solicitacao s
                WHERE s.id = e.solicitacao_id AND e.tipo_exame_id IS NULL
                  AND replace(replace(s.procedimento_sigtap_codigo,'.',''),'-','') = '0205020038';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ordem inversa: os exames voltam a ficar sem tipo (estado anterior), os tipos novos
            // saem, e o de próstata volta a se chamar obstétrico.
            migrationBuilder.Sql("""
                UPDATE smsmarica.exame_imagem SET tipo_exame_id = NULL
                WHERE tipo_exame_id IN ('b2000000-0000-0000-0000-000000000027',
                                        'b2000000-0000-0000-0000-000000000028');
                DELETE FROM smsmarica.tipo_exame
                WHERE id IN ('b2000000-0000-0000-0000-000000000027',
                             'b2000000-0000-0000-0000-000000000028');
                UPDATE smsmarica.tipo_exame SET
                    nome = 'Ultrassom obstétrico',
                    requested_procedure_description = 'US OBSTETRICO',
                    scheduled_procedure_step_description = 'Ultrassonografia obstétrica'
                WHERE procedimento_sigtap_id = 'a1000000-0000-0000-0000-000000000022';
                """);

            migrationBuilder.DeleteData(
                schema: "smsmarica",
                table: "procedimento_sigtap",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000027"));

            migrationBuilder.DeleteData(
                schema: "smsmarica",
                table: "procedimento_sigtap",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000028"));

            migrationBuilder.UpdateData(
                schema: "smsmarica",
                table: "procedimento_sigtap",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000022"),
                columns: new[] { "descricao", "nome" },
                values: new object[] { "ULTRASSONOGRAFIA OBSTETRICA", "ULTRASSONOGRAFIA OBSTETRICA" });

            migrationBuilder.UpdateData(
                schema: "smsmarica",
                table: "procedimento_sigtap",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000025"),
                column: "codigo",
                value: "02.05.02.005-4");
        }
    }
}
