using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPacienteProntuarioCompleto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "residencia_longitude",
                schema: "smsmarica",
                table: "paciente",
                type: "double precision",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<double>(
                name: "residencia_latitude",
                schema: "smsmarica",
                table: "paciente",
                type: "double precision",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AddColumn<List<string>>(
                name: "alergias",
                schema: "smsmarica",
                table: "paciente",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<int>(
                name: "altura_cm",
                schema: "smsmarica",
                table: "paciente",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "comorbidades",
                schema: "smsmarica",
                table: "paciente",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "contato_emergencia_nome",
                schema: "smsmarica",
                table: "paciente",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contato_emergencia_parentesco",
                schema: "smsmarica",
                table: "paciente",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contato_emergencia_telefone",
                schema: "smsmarica",
                table: "paciente",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "data_nascimento",
                schema: "smsmarica",
                table: "paciente",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "deficiencias",
                schema: "smsmarica",
                table: "paciente",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "email",
                schema: "smsmarica",
                table: "paciente",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_bairro",
                schema: "smsmarica",
                table: "paciente",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cep",
                schema: "smsmarica",
                table: "paciente",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cidade",
                schema: "smsmarica",
                table: "paciente",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_complemento",
                schema: "smsmarica",
                table: "paciente",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_logradouro",
                schema: "smsmarica",
                table: "paciente",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_numero",
                schema: "smsmarica",
                table: "paciente",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_ponto_referencia",
                schema: "smsmarica",
                table: "paciente",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_uf",
                schema: "smsmarica",
                table: "paciente",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "escolaridade",
                schema: "smsmarica",
                table: "paciente",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "estado_civil",
                schema: "smsmarica",
                table: "paciente",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "fator_rh",
                schema: "smsmarica",
                table: "paciente",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<List<string>>(
                name: "medicamentos_continuos",
                schema: "smsmarica",
                table: "paciente",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "nacionalidade",
                schema: "smsmarica",
                table: "paciente",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "naturalidade",
                schema: "smsmarica",
                table: "paciente",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nome_da_mae",
                schema: "smsmarica",
                table: "paciente",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nome_do_pai",
                schema: "smsmarica",
                table: "paciente",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "observacoes",
                schema: "smsmarica",
                table: "paciente",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ocupacao",
                schema: "smsmarica",
                table: "paciente",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "peso_kg",
                schema: "smsmarica",
                table: "paciente",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "plano_saude",
                schema: "smsmarica",
                table: "paciente",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "raca_cor",
                schema: "smsmarica",
                table: "paciente",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "responsavel_legal",
                schema: "smsmarica",
                table: "paciente",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rg",
                schema: "smsmarica",
                table: "paciente",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sexo",
                schema: "smsmarica",
                table: "paciente",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "telefone_celular",
                schema: "smsmarica",
                table: "paciente",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telefone_principal",
                schema: "smsmarica",
                table: "paciente",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telefone_residencial",
                schema: "smsmarica",
                table: "paciente",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tipo_sanguineo",
                schema: "smsmarica",
                table: "paciente",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_paciente_ativo",
                schema: "smsmarica",
                table: "paciente",
                column: "ativo");

            migrationBuilder.CreateIndex(
                name: "IX_paciente_nome_completo",
                schema: "smsmarica",
                table: "paciente",
                column: "nome_completo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_paciente_ativo",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropIndex(
                name: "IX_paciente_nome_completo",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "alergias",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "altura_cm",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "comorbidades",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "contato_emergencia_nome",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "contato_emergencia_parentesco",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "contato_emergencia_telefone",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "data_nascimento",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "deficiencias",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "email",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "endereco_bairro",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "endereco_cep",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "endereco_cidade",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "endereco_complemento",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "endereco_logradouro",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "endereco_numero",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "endereco_ponto_referencia",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "endereco_uf",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "escolaridade",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "estado_civil",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "fator_rh",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "medicamentos_continuos",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "nacionalidade",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "naturalidade",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "nome_da_mae",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "nome_do_pai",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "observacoes",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "ocupacao",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "peso_kg",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "plano_saude",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "raca_cor",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "responsavel_legal",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "rg",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "sexo",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "telefone_celular",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "telefone_principal",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "telefone_residencial",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "tipo_sanguineo",
                schema: "smsmarica",
                table: "paciente");

            migrationBuilder.AlterColumn<double>(
                name: "residencia_longitude",
                schema: "smsmarica",
                table: "paciente",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "residencia_latitude",
                schema: "smsmarica",
                table: "paciente",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldNullable: true);
        }
    }
}
