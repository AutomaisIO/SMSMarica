using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <summary>
    /// Backfill das opções das perguntas de lista para o envelope versionado (v1) — só DADOS, sem
    /// mudança de modelo (por isso não tem Designer: o snapshot não muda).
    ///
    /// <para>v0 (lista crua) → v1:
    /// regra    <c>["A","B"]</c> → <c>{"v":1,"opcoes":[{"id":"o1","texto":"A"},{"id":"o2","texto":"B"}]}</c>;
    /// resposta <c>["B"]</c>     → <c>{"v":1,"marcadas":[{"id":"o2","texto":"B"}]}</c> (id achado na regra
    /// pelo texto). O leitor (<c>OpcoesLista</c>) entende as duas formas, então rodar ou não rodar
    /// isto não quebra a tela — o backfill existe para o banco ficar num formato só.</para>
    ///
    /// <para>As respostas vão PRIMEIRO: o id delas sai da regra ainda em v0.</para>
    /// </summary>
    [DbContext(typeof(SmsMaisDbContext))]
    [Migration("20261001190000_OpcoesListaEnvelopeV1")]
    public partial class OpcoesListaEnvelopeV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE smsmarica.regulacao_solicitacao_resposta_regra x
                SET opcoes_marcadas_json = jsonb_build_object('v', 1, 'marcadas', (
                    SELECT jsonb_agg(jsonb_build_object('id', coalesce(o.id, 'o' || m.ord), 'texto', m.val) ORDER BY m.ord)
                    FROM jsonb_array_elements_text(x.opcoes_marcadas_json) WITH ORDINALITY AS m(val, ord)
                    LEFT JOIN LATERAL (
                        SELECT 'o' || t.ord AS id
                        FROM smsmarica.regulacao_regra g,
                             jsonb_array_elements_text(CASE WHEN jsonb_typeof(g.opcoes_json) = 'array'
                                                            THEN g.opcoes_json ELSE '[]'::jsonb END)
                                 WITH ORDINALITY AS t(val, ord)
                        WHERE g.id = x.regra_id AND t.val = m.val
                        LIMIT 1) o ON true))
                WHERE jsonb_typeof(x.opcoes_marcadas_json) = 'array';

                UPDATE smsmarica.regulacao_regra r
                SET opcoes_json = jsonb_build_object('v', 1, 'opcoes', (
                    SELECT jsonb_agg(jsonb_build_object('id', 'o' || t.ord, 'texto', t.val) ORDER BY t.ord)
                    FROM jsonb_array_elements_text(r.opcoes_json) WITH ORDINALITY AS t(val, ord)))
                WHERE jsonb_typeof(r.opcoes_json) = 'array';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sem volta de propósito: o leitor entende v0 e v1, e devolver ao formato antigo perderia
            // os ids das opções marcadas.
        }
    }
}
