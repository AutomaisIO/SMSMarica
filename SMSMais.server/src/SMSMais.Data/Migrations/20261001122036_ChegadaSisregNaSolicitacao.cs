using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChegadaSisregNaSolicitacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF NOT EXISTS de propósito. `solicitacao` é a tabela mais lida do sistema e o modelo novo
            // seleciona estas colunas em toda consulta: se o AutoMigrate do startup falhar calado, o
            // serviço sobe e TUDO que toca solicitação quebra com 42703. Por isso as colunas são
            // criadas à mão ANTES do deploy (primeiro na réplica da EVEO, que precisa tê-las para a
            // replicação lógica não travar; depois na base de produção) — e o Up() tem de continuar
            // rodável inteiro por cima do que já existe.
            migrationBuilder.Sql("""
                ALTER TABLE smsmarica.solicitacao
                    ADD COLUMN IF NOT EXISTS chegada_confirmada_sisreg boolean,
                    ADD COLUMN IF NOT EXISTS chegada_sisreg_lida_em timestamp with time zone;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE smsmarica.solicitacao
                    DROP COLUMN IF EXISTS chegada_confirmada_sisreg,
                    DROP COLUMN IF EXISTS chegada_sisreg_lida_em;
                """);
        }
    }
}
