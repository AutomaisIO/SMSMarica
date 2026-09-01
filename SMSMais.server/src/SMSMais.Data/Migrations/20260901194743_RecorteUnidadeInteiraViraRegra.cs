using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class RecorteUnidadeInteiraViraRegra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "recorte_unidade_inteira",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            // O default novo só vale para linha nova; as que já existem continuariam varrendo por
            // combinação — uma requisição por par profissional × procedimento, todo dia, contra uma
            // por unidade. Não é seed institucional (nenhum nome, marca ou CNES aqui): é a regra
            // nova alcançando as linhas que a antecederam.
            migrationBuilder.Sql(
                "UPDATE smsmarica.sisreg_varredura_agenda SET recorte_unidade_inteira = TRUE "
                + "WHERE recorte_unidade_inteira = FALSE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "recorte_unidade_inteira",
                schema: "smsmarica",
                table: "sisreg_varredura_agenda",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);
        }
    }
}
