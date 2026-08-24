using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <summary>
    /// Seed inicial de TipoExame — 13 itens cobrindo as modalidades mais comuns
    /// (Mamografia, RX, US, CT, MR). Escrito como SQL puro (não HasData) porque
    /// List&lt;string&gt; em codigos_protocolo dispara PendingModelChangesWarning a
    /// cada build quando usado via HasData.
    ///
    /// Idempotente: ON CONFLICT DO NOTHING permite reaplicar em ambientes que
    /// já receberam o seed por outra via.
    /// </summary>
    public partial class SeedTiposExameIniciais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- Mamografia (MG = 3) ----
            Inserir(migrationBuilder, "b2000000-0000-0000-0000-000000000001",
                "Mamografia bilateral de rastreamento", "a1000000-0000-0000-0000-000000000001",
                3, "MAMOGRAFIA BILATERAL - RASTREAMENTO",
                "Mamografia bilateral para rastreamento (CC + MLO)",
                new[] { "CC", "MLO" }, 15);

            Inserir(migrationBuilder, "b2000000-0000-0000-0000-000000000002",
                "Mamografia bilateral diagnóstica", "a1000000-0000-0000-0000-000000000002",
                3, "MAMOGRAFIA BILATERAL",
                "Mamografia bilateral diagnóstica",
                new[] { "CC", "MLO" }, 20);

            // ---- Radiografia (DX = 2) ----
            Inserir(migrationBuilder, "b2000000-0000-0000-0000-000000000010",
                "RX de tórax (PA + perfil)", "a1000000-0000-0000-0000-000000000011",
                2, "RX TORAX PA + PERFIL",
                "Radiografia de tórax — incidências PA e perfil",
                new[] { "PA", "LAT" }, 10);

            Inserir(migrationBuilder, "b2000000-0000-0000-0000-000000000011",
                "RX de coluna lombossacra", "a1000000-0000-0000-0000-000000000012",
                2, "RX COLUNA LOMBOSSACRA",
                "Radiografia da coluna lombossacra",
                new[] { "AP", "LAT" }, 10);

            Inserir(migrationBuilder, "b2000000-0000-0000-0000-000000000012",
                "RX de joelho", "a1000000-0000-0000-0000-000000000015",
                2, "RX JOELHO",
                "Radiografia de joelho AP + lateral",
                new[] { "AP", "LAT" }, 10);

            // ---- Ultrassom (US = 4) ----
            Inserir(migrationBuilder, "b2000000-0000-0000-0000-000000000020",
                "Ultrassom de abdome total", "a1000000-0000-0000-0000-000000000020",
                4, "US ABDOME TOTAL",
                "Ultrassonografia de abdome total",
                System.Array.Empty<string>(), 20);

            Inserir(migrationBuilder, "b2000000-0000-0000-0000-000000000021",
                "Ultrassom de tireoide", "a1000000-0000-0000-0000-000000000021",
                4, "US TIREOIDE",
                "Ultrassonografia de tireoide",
                System.Array.Empty<string>(), 15);

            Inserir(migrationBuilder, "b2000000-0000-0000-0000-000000000022",
                "Ultrassom mamário bilateral", "a1000000-0000-0000-0000-000000000025",
                4, "US MAMARIO BILATERAL",
                "Ultrassonografia mamária bilateral",
                System.Array.Empty<string>(), 20);

            Inserir(migrationBuilder, "b2000000-0000-0000-0000-000000000023",
                "Ultrassom obstétrico", "a1000000-0000-0000-0000-000000000022",
                4, "US OBSTETRICO",
                "Ultrassonografia obstétrica",
                System.Array.Empty<string>(), 25);

            // ---- Tomografia (CT = 5) ----
            Inserir(migrationBuilder, "b2000000-0000-0000-0000-000000000030",
                "Tomografia de crânio", "a1000000-0000-0000-0000-000000000030",
                5, "TC CRANIO",
                "Tomografia computadorizada do crânio",
                System.Array.Empty<string>(), 15);

            Inserir(migrationBuilder, "b2000000-0000-0000-0000-000000000031",
                "Tomografia de tórax", "a1000000-0000-0000-0000-000000000031",
                5, "TC TORAX",
                "Tomografia computadorizada de tórax",
                System.Array.Empty<string>(), 20);

            // ---- Ressonância (MR = 6) ----
            Inserir(migrationBuilder, "b2000000-0000-0000-0000-000000000040",
                "Ressonância magnética de crânio", "a1000000-0000-0000-0000-000000000040",
                6, "RM CRANIO",
                "Ressonância magnética do crânio",
                System.Array.Empty<string>(), 40);

            Inserir(migrationBuilder, "b2000000-0000-0000-0000-000000000041",
                "Ressonância magnética de coluna lombo-sacra", "a1000000-0000-0000-0000-000000000041",
                6, "RM COLUNA LOMBO-SACRA",
                "Ressonância magnética da coluna lombo-sacra",
                System.Array.Empty<string>(), 45);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM smsmarica.tipo_exame
 WHERE id IN (
    'b2000000-0000-0000-0000-000000000001'::uuid,
    'b2000000-0000-0000-0000-000000000002'::uuid,
    'b2000000-0000-0000-0000-000000000010'::uuid,
    'b2000000-0000-0000-0000-000000000011'::uuid,
    'b2000000-0000-0000-0000-000000000012'::uuid,
    'b2000000-0000-0000-0000-000000000020'::uuid,
    'b2000000-0000-0000-0000-000000000021'::uuid,
    'b2000000-0000-0000-0000-000000000022'::uuid,
    'b2000000-0000-0000-0000-000000000023'::uuid,
    'b2000000-0000-0000-0000-000000000030'::uuid,
    'b2000000-0000-0000-0000-000000000031'::uuid,
    'b2000000-0000-0000-0000-000000000040'::uuid,
    'b2000000-0000-0000-0000-000000000041'::uuid
 );");
        }

        private static void Inserir(
            MigrationBuilder mb,
            string id,
            string nome,
            string procedimentoSigtapId,
            int modalidadeDicom,
            string requestedDesc,
            string scheduledDesc,
            string[] protocolos,
            int tempoMinutos)
        {
            var protoArray = protocolos.Length == 0
                ? "ARRAY[]::text[]"
                : "ARRAY[" + string.Join(",", System.Linq.Enumerable.Select(protocolos, p => $"'{p.Replace("'", "''")}'")) + "]::text[]";

            mb.Sql($@"
INSERT INTO smsmarica.tipo_exame (
    id, nome, procedimento_sigtap_id, modalidade_dicom,
    requested_procedure_description, scheduled_procedure_step_description,
    codigos_protocolo, tempo_estimado_minutos, unidade_padrao_id,
    ativo, criado_em, criado_por, atualizado_em, atualizado_por, excluido_em, excluido_por
) VALUES (
    '{id}'::uuid, '{nome.Replace("'", "''")}',
    '{procedimentoSigtapId}'::uuid, {modalidadeDicom},
    '{requestedDesc.Replace("'", "''")}',
    '{scheduledDesc.Replace("'", "''")}',
    {protoArray}, {tempoMinutos}, NULL,
    true, TIMESTAMPTZ '2025-01-01 00:00:00+00', NULL, NULL, NULL, NULL, NULL
)
ON CONFLICT (id) DO NOTHING;");
        }
    }
}
