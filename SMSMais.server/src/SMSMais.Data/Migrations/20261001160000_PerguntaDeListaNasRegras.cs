using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <summary>
    /// Pergunta de lista nas regras de elegibilidade: as opções da regra e as opções que o
    /// solicitante marcou. O manual escreve critério alternativo como lista ("portadores das
    /// seguintes condições: …"), e cada item virando pergunta própria somava tudo com E.
    /// </summary>
    public partial class PerguntaDeListaNasRegras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF NOT EXISTS de propósito, como em ChegadaSisregNaSolicitacao: enquanto a replicação
            // lógica DO → EVEO estiver ligada, a coluna nasce à mão primeiro na réplica (senão a
            // replicação trava) e depois na produção — e o Up() tem de rodar inteiro por cima.
            migrationBuilder.Sql("""
                ALTER TABLE smsmarica.regulacao_regra
                    ADD COLUMN IF NOT EXISTS opcoes_json jsonb;
                ALTER TABLE smsmarica.regulacao_solicitacao_resposta_regra
                    ADD COLUMN IF NOT EXISTS opcoes_marcadas_json jsonb;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE smsmarica.regulacao_regra
                    DROP COLUMN IF EXISTS opcoes_json;
                ALTER TABLE smsmarica.regulacao_solicitacao_resposta_regra
                    DROP COLUMN IF EXISTS opcoes_marcadas_json;
                """);
        }
    }
}
