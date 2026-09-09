using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class DescricaoMaxPorEquipamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "descricao_max_caracteres",
                schema: "smsmarica",
                table: "equipamento",
                type: "integer",
                nullable: false,
                defaultValue: 64);

            migrationBuilder.AddCheckConstraint(
                name: "ck_equipamento_descricao_max",
                schema: "smsmarica",
                table: "equipamento",
                sql: "descricao_max_caracteres BETWEEN 4 AND 64");

            // O único aparelho que não aguenta o padrão: o mamógrafo Fuji FDR-3000AWS falha ao
            // montar a imagem (erro 31027) com descrição longa. Até aqui esse limite de 16 valia
            // para a rede inteira e truncava 189 dos 205 tipos de exame no meio da palavra.
            //
            // Vai como UPDATE preso ao AE Title, e não como INSERT: numa instância de outro
            // município o WHERE não casa e a migration não faz nada — nenhum dado institucional
            // de Maricá é criado aqui (ADR-0043). Se um dia o Fuji sair do ar, a linha some junto
            // com o equipamento.
            migrationBuilder.Sql("""
                UPDATE smsmarica.equipamento
                   SET descricao_max_caracteres = 16
                 WHERE identificador_dicom = 'FDR-MAMO';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_equipamento_descricao_max",
                schema: "smsmarica",
                table: "equipamento");

            migrationBuilder.DropColumn(
                name: "descricao_max_caracteres",
                schema: "smsmarica",
                table: "equipamento");
        }
    }
}
