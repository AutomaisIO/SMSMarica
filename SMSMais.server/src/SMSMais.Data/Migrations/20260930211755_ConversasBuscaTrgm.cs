using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <summary>
    /// A busca da lista de Conversas deixa de aplicar <c>unaccent()</c> em todas as mensagens.
    ///
    /// <c>ConversaService.ListarAsync</c> procura o termo também no CONTEÚDO das mensagens
    /// (EXISTS em <c>whatsapp_mensagem</c>). Com <c>unaccent()</c> da extensão (STABLE, não
    /// indexável) isso era varredura das ~200 mil mensagens a cada requisição — ~20 s cada, com
    /// ~10 empilhadas em 30/09/2026, porque a lista é refeita a cada tecla e a cada evento do
    /// SignalR. A consulta passou a usar <c>smsmarica.f_unaccent</c> (IMMUTABLE), e aqui nasce o GIN
    /// trigram que a atende.
    ///
    /// <c>f_unaccent</c> já existe em produção (criada pela AddPatientTrgmSearch do Automais.Fhir),
    /// então ela só é criada se faltar — nunca substituída, para as duas migrations não brigarem
    /// pela definição. Quando falta (instância nova, banco dos testes), aponta para o schema onde a
    /// extensão <c>unaccent</c> estiver de fato; nos testes ela pode não estar no smsmarica.
    ///
    /// Idempotente: em produção o índice foi criado à mão com CREATE INDEX CONCURRENTLY (sem travar
    /// escrita) e aqui vira no-op; numa instância nova a tabela nasce vazia e é instantâneo.
    /// </summary>
    /// <inheritdoc />
    public partial class ConversasBuscaTrgm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // gin_trgm_ops resolve via search_path; pg_trgm nova nasce no smsmarica, como em produção.
            migrationBuilder.Sql("SET LOCAL search_path = smsmarica, public;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.Sql(@"
DO $do$
DECLARE esquema text;
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
        WHERE n.nspname = 'smsmarica' AND p.proname = 'f_unaccent')
    THEN
        SELECT n.nspname INTO esquema
        FROM pg_extension e JOIN pg_namespace n ON n.oid = e.extnamespace
        WHERE e.extname = 'unaccent';
        EXECUTE format(
            'CREATE FUNCTION smsmarica.f_unaccent(text) RETURNS text
                 LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT AS
             $f$ SELECT %1$I.unaccent(%2$L::regdictionary, $1) $f$',
            esquema, esquema || '.unaccent');
    END IF;
END
$do$;");

            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_whatsapp_mensagem_conteudo_trgm
    ON smsmarica.whatsapp_mensagem USING gin (smsmarica.f_unaccent(conteudo) gin_trgm_ops)
    WHERE conteudo IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS smsmarica.ix_whatsapp_mensagem_conteudo_trgm;");
            // f_unaccent e pg_trgm ficam: são usados também pela busca de paciente do Automais.Fhir.
        }
    }
}
