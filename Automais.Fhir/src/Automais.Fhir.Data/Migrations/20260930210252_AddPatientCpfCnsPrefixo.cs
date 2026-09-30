using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Fhir.Data.Migrations
{
    /// <summary>
    /// Busca de paciente com dígitos (CPF/CNS por prefixo) deixa de varrer <c>fhir.patient</c>.
    ///
    /// A busca humana unificada do <c>PatientService</c> gera
    /// <c>f_unaccent(nome) ILIKE $1 OR cpf LIKE 'x%' OR cns LIKE 'x%'</c>. O ramo de nome tem o
    /// trigram da AddPatientTrgmSearch, mas os btree <c>IX_patient_cpf</c>/<c>IX_patient_cns</c>
    /// NÃO servem <c>LIKE 'x%'</c> porque o banco usa collation <c>en_US.UTF-8</c>; com um ramo do
    /// OR sem índice, o planner abandona o BitmapOr e percorre a tabela inteira. Medido em
    /// 30/09/2026: uma dessas buscas rodava havia 1 min 27 s sob carga. Dispara a partir da caixa
    /// de busca de Solicitações, Laudos, Consultas e Confirmações (via PacienteResolver) sempre que
    /// o termo tem dígitos.
    ///
    /// <c>varchar_pattern_ops</c> é o operador que serve prefixo com collation não-C. Parcial em
    /// <c>IS NOT NULL</c>: <c>LIKE</c> é estrito, então o planner prova a implicação.
    ///
    /// Idempotente (IF NOT EXISTS): em produção os índices já foram criados à mão com
    /// CREATE INDEX CONCURRENTLY (sem travar escrita), então aqui vira no-op; numa instância nova a
    /// tabela nasce vazia e o índice é instantâneo.
    /// </summary>
    /// <inheritdoc />
    public partial class AddPatientCpfCnsPrefixo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_patient_cpf_prefixo
    ON fhir.patient (cpf varchar_pattern_ops) WHERE cpf IS NOT NULL;");

            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_patient_cns_prefixo
    ON fhir.patient (cns varchar_pattern_ops) WHERE cns IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS fhir.ix_patient_cns_prefixo;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS fhir.ix_patient_cpf_prefixo;");
        }
    }
}
