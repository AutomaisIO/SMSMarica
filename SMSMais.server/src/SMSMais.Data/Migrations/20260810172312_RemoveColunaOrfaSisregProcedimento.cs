using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <summary>
    /// Remove <c>tipo_exame.sisreg_procedimento_id</c>, que só existia em produção.
    ///
    /// <para>Ela veio da migration <c>20260806184705_EixoProcedimentoSisreg</c>, aplicada no banco
    /// em 06/08/2026 mas cujo código ficou num <c>git stash</c> e nunca entrou no repositório —
    /// está no <c>__migrations</c> e em branch nenhum. A ideia dela era ancorar o tipo de exame na
    /// linha do de-para do SISREG; o eixo acabou sendo o NOME do procedimento
    /// (<c>ProcedimentoSisregEhOFiel</c>, 10/08/2026), então a coluna nunca foi lida nem escrita:
    /// 0 valores em 55 tipos, nenhuma referência no código.</para>
    ///
    /// <para><b>Esta migração é vazia para o EF de propósito.</b> A coluna nunca esteve no modelo,
    /// então não há mudança de snapshot a gerar — o scaffold sai em branco e o SQL é escrito à mão.
    /// E ele é <c>IF EXISTS</c> porque em banco novo a coluna jamais existiu: nenhuma migração
    /// deste repositório a cria.</para>
    /// </summary>
    public partial class RemoveColunaOrfaSisregProcedimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A FK e o índice caem junto com a coluna — o Postgres os remove em cascata.
            migrationBuilder.Sql(@"
                ALTER TABLE smsmarica.tipo_exame
                    DROP COLUMN IF EXISTS sisreg_procedimento_id;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op deliberado. Recriar a coluna devolveria a produção ao estado anterior, mas daria
            // a um banco novo uma coluna que ele nunca teve — e que nenhuma migração daqui cria.
            // Não há dado a recuperar: ela estava vazia.
        }
    }
}
