using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <summary>
    /// Uma lista só para aviso de erro e falha da plataforma. Os telefones que estavam cadastrados
    /// por integração (<c>telefonesNotificacao</c> no <c>parametros_json</c> da credencial do
    /// SISREG/SER/SERNIT) passam para <c>alerta_destinatario</c> — a lista de Sistema → Avisos no
    /// celular — e saem da credencial. Só dados; o modelo não muda (sem Designer/snapshot).
    ///
    /// <para>Por quê: em 24/09/2026 o operador tinha o celular só na lista do SISREG; o robô ficou
    /// três dias parado e os 2.377 avisos morreram em "nenhum telefone cadastrado".</para>
    ///
    /// <para>Não tem telefone de ninguém aqui: copia o que JÁ está no banco de cada instância.
    /// Instância sem telefone por integração não muda nada.</para>
    /// </summary>
    [DbContext(typeof(SmsMaisDbContext))]
    [Migration("20260924220000_UnificarTelefonesAvisos")]
    public partial class UnificarTelefonesAvisos : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mesma régua da tela: só dígitos, 10 a 13. Repetido = mesmos 10 últimos dígitos
            // (com e sem o 55 são o mesmo celular).
            migrationBuilder.Sql("""
                INSERT INTO smsmarica.alerta_destinatario (id, telefone, nome, ativo, criado_em, criado_por)
                SELECT gen_random_uuid(), t.fone, 'Vindo da integração ' || upper(t.provedor), true, now(), 'migração'
                FROM (
                    SELECT DISTINCT ON (right(regexp_replace(f.valor, '\D', '', 'g'), 10))
                           regexp_replace(f.valor, '\D', '', 'g') AS fone, c.provedor
                    FROM smsmarica.integracao_credencial c
                    CROSS JOIN LATERAL jsonb_array_elements_text(c.parametros_json -> 'telefonesNotificacao') AS f(valor)
                    WHERE jsonb_typeof(c.parametros_json -> 'telefonesNotificacao') = 'array'
                    ORDER BY right(regexp_replace(f.valor, '\D', '', 'g'), 10), c.provedor
                ) t
                WHERE length(t.fone) BETWEEN 10 AND 13
                  AND NOT EXISTS (
                      SELECT 1 FROM smsmarica.alerta_destinatario d
                      WHERE right(d.telefone, 10) = right(t.fone, 10));
                """);

            migrationBuilder.Sql("""
                UPDATE smsmarica.integracao_credencial
                SET parametros_json = parametros_json - 'telefonesNotificacao'
                WHERE parametros_json ? 'telefonesNotificacao';
                """);
        }

        // Sem volta: a lista por integração deixou de existir no código.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
