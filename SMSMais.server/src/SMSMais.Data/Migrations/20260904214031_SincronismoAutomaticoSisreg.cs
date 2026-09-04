using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class SincronismoAutomaticoSisreg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // `defaultValue: true` aqui e DROP DEFAULT logo abaixo, de propósito, em dois tempos:
            //
            // 1. A linha singleton de `sisreg_configuracao` já existe em produção com o sincronismo
            //    rodando. Uma coluna nova NOT NULL sem default nasceria `false` e este deploy — que
            //    só queria expor o botão — desligaria a rede inteira sem ninguém pedir.
            //
            // 2. Feito o backfill, o default sai. Num `bool`, o sentinela do EF é `false`, então uma
            //    coluna que mantivesse default `true` faria um INSERT com `false` gravar `true`: o
            //    desligamento sumiria exatamente na linha que precisa criá-lo. Sem default, o valor
            //    sempre vem explícito do EF.
            migrationBuilder.AddColumn<bool>(
                name: "sincronismo_automatico_ativo",
                schema: "smsmarica",
                table: "sisreg_configuracao",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql(
                "ALTER TABLE smsmarica.sisreg_configuracao "
                + "ALTER COLUMN sincronismo_automatico_ativo DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sincronismo_automatico_ativo",
                schema: "smsmarica",
                table: "sisreg_configuracao");
        }
    }
}
