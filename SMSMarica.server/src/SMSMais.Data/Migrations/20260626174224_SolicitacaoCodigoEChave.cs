using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class SolicitacaoCodigoEChave : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserva os dados: o antigo "Número de Regulação" vira "Código de Solicitação".
            migrationBuilder.RenameColumn(
                name: "numero_regulacao_sus",
                schema: "smsmarica",
                table: "solicitacao_exame",
                newName: "codigo_solicitacao");

            migrationBuilder.AlterColumn<string>(
                name: "codigo_solicitacao",
                schema: "smsmarica",
                table: "solicitacao_exame",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "chave_confirmacao",
                schema: "smsmarica",
                table: "solicitacao_exame",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // Ajuste da base existente (regra acordada):
            //  - registros sem código (regulação nula) recebem o sentinela 0000 (extra-SUS);
            //  - todos os registros existentes recebem chave 0000 (não havia chave no modelo antigo).
            migrationBuilder.Sql(
                "UPDATE smsmarica.solicitacao_exame SET codigo_solicitacao = '0000' " +
                "WHERE codigo_solicitacao IS NULL OR btrim(codigo_solicitacao) = '';");
            migrationBuilder.Sql(
                "UPDATE smsmarica.solicitacao_exame SET chave_confirmacao = '0000' " +
                "WHERE chave_confirmacao IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "chave_confirmacao",
                schema: "smsmarica",
                table: "solicitacao_exame");

            migrationBuilder.AlterColumn<string>(
                name: "codigo_solicitacao",
                schema: "smsmarica",
                table: "solicitacao_exame",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "codigo_solicitacao",
                schema: "smsmarica",
                table: "solicitacao_exame",
                newName: "numero_regulacao_sus");
        }
    }
}
