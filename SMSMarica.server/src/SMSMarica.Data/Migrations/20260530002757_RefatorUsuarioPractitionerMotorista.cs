using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefatorUsuarioPractitionerMotorista : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_laudo_medico_medico_id",
                schema: "smsmarica",
                table: "laudo");

            migrationBuilder.DropForeignKey(
                name: "FK_motorista_usuario_usuario_id",
                schema: "smsmarica",
                table: "motorista");

            migrationBuilder.DropTable(
                name: "medico",
                schema: "smsmarica");

            migrationBuilder.DropIndex(
                name: "IX_usuario_cpf",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropIndex(
                name: "IX_motorista_usuario_id",
                schema: "smsmarica",
                table: "motorista");

            migrationBuilder.DropColumn(
                name: "cpf",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "data_nascimento",
                schema: "smsmarica",
                table: "usuario");

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
                name: "foto_base64",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "rg",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "sexo",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "telefone",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "usuario_id",
                schema: "smsmarica",
                table: "motorista");

            migrationBuilder.RenameColumn(
                name: "nome_completo",
                schema: "smsmarica",
                table: "usuario",
                newName: "nome_exibicao");

            migrationBuilder.RenameColumn(
                name: "medico_uf_crm_snapshot",
                schema: "smsmarica",
                table: "laudo",
                newName: "practitioner_uf_crm_snapshot");

            migrationBuilder.RenameColumn(
                name: "medico_rqe_snapshot",
                schema: "smsmarica",
                table: "laudo",
                newName: "practitioner_rqe_snapshot");

            migrationBuilder.RenameColumn(
                name: "medico_nome_snapshot",
                schema: "smsmarica",
                table: "laudo",
                newName: "practitioner_nome_snapshot");

            migrationBuilder.RenameColumn(
                name: "medico_id",
                schema: "smsmarica",
                table: "laudo",
                newName: "practitioner_id");

            migrationBuilder.RenameColumn(
                name: "medico_crm_snapshot",
                schema: "smsmarica",
                table: "laudo",
                newName: "practitioner_crm_snapshot");

            migrationBuilder.RenameIndex(
                name: "IX_laudo_medico_id",
                schema: "smsmarica",
                table: "laudo",
                newName: "IX_laudo_practitioner_id");

            migrationBuilder.AddColumn<Guid>(
                name: "motorista_id",
                schema: "smsmarica",
                table: "usuario",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "patient_id",
                schema: "smsmarica",
                table: "usuario",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "practitioner_id",
                schema: "smsmarica",
                table: "usuario",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cpf",
                schema: "smsmarica",
                table: "motorista",
                type: "character varying(11)",
                maxLength: 11,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "data_nascimento",
                schema: "smsmarica",
                table: "motorista",
                type: "date",
                nullable: true);

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

            migrationBuilder.AddColumn<string>(
                name: "foto_base64",
                schema: "smsmarica",
                table: "motorista",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nome_completo",
                schema: "smsmarica",
                table: "motorista",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "rg",
                schema: "smsmarica",
                table: "motorista",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sexo",
                schema: "smsmarica",
                table: "motorista",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telefone",
                schema: "smsmarica",
                table: "motorista",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuario_motorista_id",
                schema: "smsmarica",
                table: "usuario",
                column: "motorista_id",
                unique: true,
                filter: "motorista_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_patient_id",
                schema: "smsmarica",
                table: "usuario",
                column: "patient_id",
                unique: true,
                filter: "patient_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_practitioner_id",
                schema: "smsmarica",
                table: "usuario",
                column: "practitioner_id",
                unique: true,
                filter: "practitioner_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_usuario_papel_unico",
                schema: "smsmarica",
                table: "usuario",
                sql: "(CASE WHEN patient_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN practitioner_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN motorista_id IS NOT NULL THEN 1 ELSE 0 END) <= 1");

            migrationBuilder.CreateIndex(
                name: "IX_motorista_cpf",
                schema: "smsmarica",
                table: "motorista",
                column: "cpf",
                unique: true,
                filter: "cpf IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_laudo_practitioner_practitioner_id",
                schema: "smsmarica",
                table: "laudo",
                column: "practitioner_id",
                principalSchema: "fhir",
                principalTable: "practitioner",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_usuario_motorista_motorista_id",
                schema: "smsmarica",
                table: "usuario",
                column: "motorista_id",
                principalSchema: "smsmarica",
                principalTable: "motorista",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_usuario_patient_patient_id",
                schema: "smsmarica",
                table: "usuario",
                column: "patient_id",
                principalSchema: "fhir",
                principalTable: "patient",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_usuario_practitioner_practitioner_id",
                schema: "smsmarica",
                table: "usuario",
                column: "practitioner_id",
                principalSchema: "fhir",
                principalTable: "practitioner",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_laudo_practitioner_practitioner_id",
                schema: "smsmarica",
                table: "laudo");

            migrationBuilder.DropForeignKey(
                name: "FK_usuario_motorista_motorista_id",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropForeignKey(
                name: "FK_usuario_patient_patient_id",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropForeignKey(
                name: "FK_usuario_practitioner_practitioner_id",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropIndex(
                name: "IX_usuario_motorista_id",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropIndex(
                name: "IX_usuario_patient_id",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropIndex(
                name: "IX_usuario_practitioner_id",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropCheckConstraint(
                name: "ck_usuario_papel_unico",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropIndex(
                name: "IX_motorista_cpf",
                schema: "smsmarica",
                table: "motorista");

            migrationBuilder.DropColumn(
                name: "motorista_id",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "patient_id",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "practitioner_id",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "cpf",
                schema: "smsmarica",
                table: "motorista");

            migrationBuilder.DropColumn(
                name: "data_nascimento",
                schema: "smsmarica",
                table: "motorista");

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

            migrationBuilder.DropColumn(
                name: "foto_base64",
                schema: "smsmarica",
                table: "motorista");

            migrationBuilder.DropColumn(
                name: "nome_completo",
                schema: "smsmarica",
                table: "motorista");

            migrationBuilder.DropColumn(
                name: "rg",
                schema: "smsmarica",
                table: "motorista");

            migrationBuilder.DropColumn(
                name: "sexo",
                schema: "smsmarica",
                table: "motorista");

            migrationBuilder.DropColumn(
                name: "telefone",
                schema: "smsmarica",
                table: "motorista");

            migrationBuilder.RenameColumn(
                name: "nome_exibicao",
                schema: "smsmarica",
                table: "usuario",
                newName: "nome_completo");

            migrationBuilder.RenameColumn(
                name: "practitioner_uf_crm_snapshot",
                schema: "smsmarica",
                table: "laudo",
                newName: "medico_uf_crm_snapshot");

            migrationBuilder.RenameColumn(
                name: "practitioner_rqe_snapshot",
                schema: "smsmarica",
                table: "laudo",
                newName: "medico_rqe_snapshot");

            migrationBuilder.RenameColumn(
                name: "practitioner_nome_snapshot",
                schema: "smsmarica",
                table: "laudo",
                newName: "medico_nome_snapshot");

            migrationBuilder.RenameColumn(
                name: "practitioner_id",
                schema: "smsmarica",
                table: "laudo",
                newName: "medico_id");

            migrationBuilder.RenameColumn(
                name: "practitioner_crm_snapshot",
                schema: "smsmarica",
                table: "laudo",
                newName: "medico_crm_snapshot");

            migrationBuilder.RenameIndex(
                name: "IX_laudo_practitioner_id",
                schema: "smsmarica",
                table: "laudo",
                newName: "IX_laudo_medico_id");

            migrationBuilder.AddColumn<string>(
                name: "cpf",
                schema: "smsmarica",
                table: "usuario",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "data_nascimento",
                schema: "smsmarica",
                table: "usuario",
                type: "date",
                nullable: true);

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
                name: "foto_base64",
                schema: "smsmarica",
                table: "usuario",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rg",
                schema: "smsmarica",
                table: "usuario",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sexo",
                schema: "smsmarica",
                table: "usuario",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telefone",
                schema: "smsmarica",
                table: "usuario",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "usuario_id",
                schema: "smsmarica",
                table: "motorista",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "medico",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    crm = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    especialidade = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true),
                    rqe = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    uf_crm = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    validade_crm = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medico", x => x.id);
                    table.ForeignKey(
                        name: "FK_medico_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "smsmarica",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_usuario_cpf",
                schema: "smsmarica",
                table: "usuario",
                column: "cpf",
                unique: true,
                filter: "cpf IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_motorista_usuario_id",
                schema: "smsmarica",
                table: "motorista",
                column: "usuario_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_medico_crm_uf_crm",
                schema: "smsmarica",
                table: "medico",
                columns: new[] { "crm", "uf_crm" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_medico_excluido_em",
                schema: "smsmarica",
                table: "medico",
                column: "excluido_em",
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_medico_usuario_id",
                schema: "smsmarica",
                table: "medico",
                column: "usuario_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_laudo_medico_medico_id",
                schema: "smsmarica",
                table: "laudo",
                column: "medico_id",
                principalSchema: "smsmarica",
                principalTable: "medico",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_motorista_usuario_usuario_id",
                schema: "smsmarica",
                table: "motorista",
                column: "usuario_id",
                principalSchema: "smsmarica",
                principalTable: "usuario",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
