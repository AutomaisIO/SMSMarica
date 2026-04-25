using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarEnderecosUnidadeMotoristaUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "endereco_bairro",
                schema: "smsmarica",
                table: "usuario",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cep",
                schema: "smsmarica",
                table: "usuario",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cidade",
                schema: "smsmarica",
                table: "usuario",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_complemento",
                schema: "smsmarica",
                table: "usuario",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_logradouro",
                schema: "smsmarica",
                table: "usuario",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_numero",
                schema: "smsmarica",
                table: "usuario",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_ponto_referencia",
                schema: "smsmarica",
                table: "usuario",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_uf",
                schema: "smsmarica",
                table: "usuario",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telefone",
                schema: "smsmarica",
                table: "usuario",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "longitude",
                schema: "smsmarica",
                table: "unidade",
                type: "double precision",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<double>(
                name: "latitude",
                schema: "smsmarica",
                table: "unidade",
                type: "double precision",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AddColumn<string>(
                name: "endereco_bairro",
                schema: "smsmarica",
                table: "unidade",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cep",
                schema: "smsmarica",
                table: "unidade",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cidade",
                schema: "smsmarica",
                table: "unidade",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_complemento",
                schema: "smsmarica",
                table: "unidade",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_logradouro",
                schema: "smsmarica",
                table: "unidade",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_numero",
                schema: "smsmarica",
                table: "unidade",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_ponto_referencia",
                schema: "smsmarica",
                table: "unidade",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_uf",
                schema: "smsmarica",
                table: "unidade",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            // Preservar o endereço livre das unidades existentes: joga pro novo campo logradouro.
            migrationBuilder.Sql(
                "UPDATE smsmarica.unidade SET endereco_logradouro = LEFT(endereco, 200) " +
                "WHERE endereco IS NOT NULL AND endereco <> '';");

            migrationBuilder.DropColumn(
                name: "endereco",
                schema: "smsmarica",
                table: "unidade");

            migrationBuilder.AddColumn<string>(
                name: "endereco_bairro",
                schema: "smsmarica",
                table: "motorista",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cep",
                schema: "smsmarica",
                table: "motorista",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cidade",
                schema: "smsmarica",
                table: "motorista",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_complemento",
                schema: "smsmarica",
                table: "motorista",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_logradouro",
                schema: "smsmarica",
                table: "motorista",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_numero",
                schema: "smsmarica",
                table: "motorista",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_ponto_referencia",
                schema: "smsmarica",
                table: "motorista",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_uf",
                schema: "smsmarica",
                table: "motorista",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "endereco_bairro",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "endereco_cep",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "endereco_cidade",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "endereco_complemento",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "endereco_logradouro",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "endereco_numero",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "endereco_ponto_referencia",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "endereco_uf",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "telefone",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "endereco_bairro",
                schema: "smsmarica",
                table: "unidade");

            migrationBuilder.DropColumn(
                name: "endereco_cep",
                schema: "smsmarica",
                table: "unidade");

            migrationBuilder.DropColumn(
                name: "endereco_cidade",
                schema: "smsmarica",
                table: "unidade");

            migrationBuilder.DropColumn(
                name: "endereco_complemento",
                schema: "smsmarica",
                table: "unidade");

            migrationBuilder.DropColumn(
                name: "endereco_logradouro",
                schema: "smsmarica",
                table: "unidade");

            migrationBuilder.DropColumn(
                name: "endereco_numero",
                schema: "smsmarica",
                table: "unidade");

            migrationBuilder.DropColumn(
                name: "endereco_ponto_referencia",
                schema: "smsmarica",
                table: "unidade");

            migrationBuilder.DropColumn(
                name: "endereco_uf",
                schema: "smsmarica",
                table: "unidade");

            migrationBuilder.DropColumn(
                name: "endereco_bairro",
                schema: "smsmarica",
                table: "motorista");

            migrationBuilder.DropColumn(
                name: "endereco_cep",
                schema: "smsmarica",
                table: "motorista");

            migrationBuilder.DropColumn(
                name: "endereco_cidade",
                schema: "smsmarica",
                table: "motorista");

            migrationBuilder.DropColumn(
                name: "endereco_complemento",
                schema: "smsmarica",
                table: "motorista");

            migrationBuilder.DropColumn(
                name: "endereco_logradouro",
                schema: "smsmarica",
                table: "motorista");

            migrationBuilder.DropColumn(
                name: "endereco_numero",
                schema: "smsmarica",
                table: "motorista");

            migrationBuilder.DropColumn(
                name: "endereco_ponto_referencia",
                schema: "smsmarica",
                table: "motorista");

            migrationBuilder.DropColumn(
                name: "endereco_uf",
                schema: "smsmarica",
                table: "motorista");

            migrationBuilder.AlterColumn<double>(
                name: "longitude",
                schema: "smsmarica",
                table: "unidade",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "latitude",
                schema: "smsmarica",
                table: "unidade",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco",
                schema: "smsmarica",
                table: "unidade",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }
    }
}
