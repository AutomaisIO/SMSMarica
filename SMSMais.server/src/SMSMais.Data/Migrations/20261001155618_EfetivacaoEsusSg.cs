using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class EfetivacaoEsusSg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF NOT EXISTS: as colunas são criadas à mão antes do deploy (réplica da EVEO, que precisa
            // tê-las para a replicação lógica não travar, e produção) — o Up() continua rodável por cima.
            migrationBuilder.Sql("""
                ALTER TABLE smsmarica.esussg_solicitacao
                    ADD COLUMN IF NOT EXISTS efetivacao integer,
                    ADD COLUMN IF NOT EXISTS efetivado_em timestamp with time zone,
                    ADD COLUMN IF NOT EXISTS motivo_nao_efetivacao character varying(200),
                    ADD COLUMN IF NOT EXISTS efetivacao_lida_em timestamp with time zone;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE smsmarica.esussg_solicitacao
                    DROP COLUMN IF EXISTS efetivacao,
                    DROP COLUMN IF EXISTS efetivado_em,
                    DROP COLUMN IF EXISTS motivo_nao_efetivacao,
                    DROP COLUMN IF EXISTS efetivacao_lida_em;
                """);
        }
    }
}
