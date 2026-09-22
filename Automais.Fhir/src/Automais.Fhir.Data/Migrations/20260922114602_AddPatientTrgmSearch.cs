using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Fhir.Data.Migrations
{
    /// <summary>
    /// Busca de paciente por nome/telefone deixa de ser seq scan em ~378k linhas.
    ///
    /// A query gera <c>f_unaccent(nome) ILIKE '%termo%'</c> e <c>telefone LIKE '%dígitos%'</c>
    /// (curinga à esquerda) — nenhum índice B-tree serve. Solução: <c>pg_trgm</c> + índices GIN
    /// trigram. Como <c>unaccent(text)</c> da extensão é <c>STABLE</c> (e pertence ao papel
    /// <c>postgres</c>, não dá para marcá-la IMMUTABLE), o índice de nome é sobre um wrapper
    /// IMMUTABLE próprio, <c>smsmarica.f_unaccent</c>, que o <c>PatientService</c> passou a chamar.
    ///
    /// Idempotente (IF NOT EXISTS / CREATE OR REPLACE): em produção os objetos já foram criados
    /// à mão com CREATE INDEX CONCURRENTLY (sem lock), então aqui vira no-op; numa instância nova
    /// (whitelabel) a tabela nasce vazia e o índice é instantâneo. As funções/extensões vivem no
    /// schema smsmarica — mesmo acoplamento de runtime que o serviço já tem (ver Program.cs).
    /// </summary>
    /// <inheritdoc />
    public partial class AddPatientTrgmSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // gin_trgm_ops (pg_trgm) e o dicionário unaccent resolvem via search_path.
            migrationBuilder.Sql("SET LOCAL search_path = smsmarica, fhir, public;");

            // unaccent é criado pela migration do SMSMais.server; garantido aqui para a fhir
            // migration ser autossuficiente independente da ordem de deploy numa instância nova.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent SCHEMA smsmarica;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm SCHEMA smsmarica;");

            // Wrapper IMMUTABLE de unaccent (forma 2-arg com dicionário explícito = indexável).
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION smsmarica.f_unaccent(text) RETURNS text
    LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT AS
$func$ SELECT smsmarica.unaccent('smsmarica.unaccent'::regdictionary, $1) $func$;");

            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_patient_nome_funaccent_trgm
    ON fhir.patient USING gin (smsmarica.f_unaccent(nome) gin_trgm_ops);");

            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_patient_telefone_trgm
    ON fhir.patient USING gin (telefone gin_trgm_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS fhir.ix_patient_telefone_trgm;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS fhir.ix_patient_nome_funaccent_trgm;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS smsmarica.f_unaccent(text);");
            // pg_trgm/unaccent preservados de propósito: podem ser usados por outros objetos.
        }
    }
}
