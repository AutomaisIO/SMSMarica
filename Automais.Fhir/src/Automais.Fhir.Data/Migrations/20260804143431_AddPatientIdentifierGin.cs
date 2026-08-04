using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Fhir.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientIdentifierGin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Índice GIN de EXPRESSÃO sobre os identifiers do Patient — atende a busca exata
            // por identifier de qualquer system ((content->'identifier') @> [{system,value}]).
            // É o que permite aos conectores reencontrarem paciente SEM CPF pela chave local
            // da base de origem; sem isso a busca caía no filtro de CPF e voltava vazia, e
            // cada ciclo incremental criava uma cópia nova do mesmo paciente (medido em
            // 04/08/2026: 25 pacientes do Salux tinham virado 260 recursos).
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_patient_identifier_gin " +
                "ON fhir.patient USING GIN ((content->'identifier') jsonb_path_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS fhir.ix_patient_identifier_gin;");
        }
    }
}
