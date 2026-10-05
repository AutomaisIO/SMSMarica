using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <summary>
    /// Consultas e exames passaram a ser uma lista só (módulo 16, <c>Solicitacoes</c>); o módulo 41
    /// (<c>Consultas</c>) deixa de existir. Quem tinha o 41 recebe as mesmas ações no 16 — somadas
    /// às que já tinha — e o 41 é apagado, em perfis e nas exceções por usuário. Só dados: o
    /// schema não muda.
    /// </summary>
    public partial class FundeModuloConsultasEmSolicitacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO smsmarica.permissao_perfil (perfil_id, modulo, acoes)
                SELECT perfil_id, 16, acoes FROM smsmarica.permissao_perfil WHERE modulo = 41
                ON CONFLICT (perfil_id, modulo)
                DO UPDATE SET acoes = smsmarica.permissao_perfil.acoes | EXCLUDED.acoes;

                DELETE FROM smsmarica.permissao_perfil WHERE modulo = 41;

                INSERT INTO smsmarica.permissao_usuario (usuario_id, modulo, acoes)
                SELECT usuario_id, 16, acoes FROM smsmarica.permissao_usuario WHERE modulo = 41
                ON CONFLICT (usuario_id, modulo)
                DO UPDATE SET acoes = smsmarica.permissao_usuario.acoes | EXCLUDED.acoes;

                DELETE FROM smsmarica.permissao_usuario WHERE modulo = 41;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversível: depois da fusão não há como saber quais ações do 16 vieram do 41.
        }
    }
}
