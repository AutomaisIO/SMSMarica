using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <summary>
    /// Unidade padrão obrigatória (ADR-0081). Só dados, sem mudança de schema: quem tem UMA unidade
    /// só e nenhuma marcada passa a tê-la como padrão — era o que o sistema já presumia (a "única"
    /// valia como principal). Quem tem várias e nenhuma marcada fica como está: escolher entre elas é
    /// decisão do administrador (filtro "Sem unidade padrão" na tela de Usuários). Não é seed
    /// institucional: vale igual em qualquer instância.
    /// </summary>
    public partial class UnidadePadraoObrigatoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE smsmarica.usuario_unidade uu
                SET principal = TRUE
                WHERE NOT EXISTS (
                        SELECT 1 FROM smsmarica.usuario_unidade p
                        WHERE p.usuario_id = uu.usuario_id AND p.principal)
                  AND (SELECT count(*) FROM smsmarica.usuario_unidade c
                       WHERE c.usuario_id = uu.usuario_id) = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sem volta: depois do Up não dá para distinguir a padrão marcada aqui da escolhida na
            // tela — e desmarcar a de quem já escolheu seria pior que manter.
        }
    }
}
