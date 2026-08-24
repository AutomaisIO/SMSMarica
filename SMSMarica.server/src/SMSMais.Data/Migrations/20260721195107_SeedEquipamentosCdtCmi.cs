using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <summary>
    /// Semeia os equipamentos que já operam com o PACS, agora que o AE Title da estação
    /// vem do cadastro (e não mais de configuração): mamógrafo Fuji do CDT (MG/FDR-MAMO)
    /// e ultrassom do Centro Materno Infantil (US/US_CMI).
    ///
    /// Ancorado no CNES da unidade — os ids das unidades são de produção, não constantes
    /// de seed. Em base que não tenha essas unidades (teste/CI), o INSERT não insere nada.
    /// Idempotente: não duplica se a unidade já tiver equipamento na modalidade.
    /// </summary>
    public partial class SeedEquipamentosCdtCmi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            Semear(migrationBuilder, cnes: "3132358", nome: "Mamógrafo Fuji FDR-3000AWS", modalidade: 3, ae: "FDR-MAMO");
            Semear(migrationBuilder, cnes: "2930242", nome: "Ultrassom", modalidade: 4, ae: "US_CMI");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove só o que este seed criou (identificado pelo AE Title).
            migrationBuilder.Sql(@"
                DELETE FROM smsmarica.equipamento
                WHERE identificador_dicom IN ('FDR-MAMO', 'US_CMI');");
        }

        private static void Semear(MigrationBuilder migrationBuilder, string cnes, string nome, int modalidade, string ae)
        {
            migrationBuilder.Sql($@"
                INSERT INTO smsmarica.equipamento
                    (id, nome, unidade_id, modalidade_dicom, identificador_dicom, ativo, criado_em)
                SELECT gen_random_uuid(), '{nome}', u.id, {modalidade}, '{ae}', TRUE, NOW()
                FROM smsmarica.unidade u
                WHERE u.cnes = '{cnes}'
                  AND NOT EXISTS (
                      SELECT 1 FROM smsmarica.equipamento e
                      WHERE e.unidade_id = u.id
                        AND e.modalidade_dicom = {modalidade}
                        AND e.excluido_em IS NULL);");
        }
    }
}
