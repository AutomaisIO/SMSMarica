using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <summary>
    /// Fecha o elo que faltava para a USG transvaginal chegar à worklist do ultrassom do
    /// Centro Materno Infantil (AE <c>US_CMI</c>).
    ///
    /// <para><b>1. Código SIGTAP corrigido.</b> O seed trazia a transvaginal como
    /// <c>0205020135</c>, código que não aparece uma única vez na extração de agendamentos do
    /// SISREG — enquanto <c>0205020186</c> aparece 489 vezes (as três variações que o SISREG
    /// escreve — "ULTRASONOGRAFIA TRANSVAGINAL", "USG TRANSVAGINAL - GESTANTE" e
    /// "ULTRASSONOGRAFIA TRANSVAGINAL - DIU" — colapsam no mesmo código). Nada apontava para a
    /// linha antiga (nenhum tipo de exame, nenhuma solicitação), então é troca de código e não
    /// registro novo.</para>
    ///
    /// <para><b>2. TipoExame novo.</b> Sem ele a importação do SISREG grava
    /// <c>ExameImagem.TipoExameId = null</c>, e aí <c>AutorizarAsync</c> avalia
    /// <c>TipoExame?.EnviarParaWorklist ?? false</c> como false nos dois pontos que ligam o
    /// envio: não pede a estação e não agenda <c>ProximaTentativaEm</c>. O exame ficava parado
    /// em "Solicitada" para sempre, <b>sem erro nenhum</b>. Com o tipo mapeado e
    /// <c>enviar_para_worklist = true</c>, o US do CMI (único da unidade na modalidade) é
    /// escolhido sozinho na autorização e o item MWL sai com ScheduledStationAETitle = US_CMI.</para>
    ///
    /// Idempotente (<c>ON CONFLICT DO NOTHING</c>), no mesmo padrão de SQL puro do
    /// <c>SeedTiposExameIniciais</c> — <c>HasData</c> não serve para <c>tipo_exame</c> por causa
    /// da <c>List&lt;string&gt;</c> em <c>codigos_protocolo</c>.
    /// </summary>
    public partial class SeedTipoExameUsTransvaginal : Migration
    {
        private const string TipoExameId = "b2000000-0000-0000-0000-000000000024";
        private const string SigtapTransvaginalId = "a1000000-0000-0000-0000-000000000023";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "smsmarica",
                table: "procedimento_sigtap",
                keyColumn: "id",
                keyValue: new Guid(SigtapTransvaginalId),
                column: "codigo",
                value: "02.05.02.018-6");

            // requested_procedure_description vai literal para a tag (0032,1060) e é truncada em
            // 16 caracteres no item MWL (exigência do Fuji — ver ConstrutorMwlItem.LoDesc):
            // "US TRANSVAGINAL" tem 15 e passa inteira.
            migrationBuilder.Sql($@"
INSERT INTO smsmarica.tipo_exame (
    id, nome, procedimento_sigtap_id, modalidade_dicom,
    requested_procedure_description, scheduled_procedure_step_description,
    codigos_protocolo, tempo_estimado_minutos, unidade_padrao_id,
    ativo, enviar_para_worklist,
    criado_em, criado_por, atualizado_em, atualizado_por, excluido_em, excluido_por
) VALUES (
    '{TipoExameId}'::uuid, 'Ultrassom transvaginal',
    '{SigtapTransvaginalId}'::uuid, 4,
    'US TRANSVAGINAL',
    'Ultrassonografia transvaginal',
    ARRAY[]::text[], 20, NULL,
    true, true,
    TIMESTAMPTZ '2026-07-23 00:00:00+00', NULL, NULL, NULL, NULL, NULL
)
ON CONFLICT (id) DO NOTHING;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
DELETE FROM smsmarica.tipo_exame WHERE id = '{TipoExameId}'::uuid;");

            migrationBuilder.UpdateData(
                schema: "smsmarica",
                table: "procedimento_sigtap",
                keyColumn: "id",
                keyValue: new Guid(SigtapTransvaginalId),
                column: "codigo",
                value: "02.05.02.013-5");
        }
    }
}
