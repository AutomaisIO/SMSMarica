using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class IntegrarPacienteMedicoAoFhir : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_laudo_medico_medico_id",
                schema: "smsmarica",
                table: "laudo");

            migrationBuilder.DropForeignKey(
                name: "FK_laudo_paciente_paciente_id",
                schema: "smsmarica",
                table: "laudo");

            migrationBuilder.DropForeignKey(
                name: "FK_solicitacao_exame_paciente_paciente_id",
                schema: "smsmarica",
                table: "solicitacao_exame");

            migrationBuilder.DropForeignKey(
                name: "FK_tratamento_paciente_paciente_id",
                schema: "smsmarica",
                table: "tratamento");

            migrationBuilder.DropTable(
                name: "medico",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "paciente",
                schema: "smsmarica");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateTable(
                name: "paciente",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alergias = table.Column<List<string>>(type: "text[]", nullable: false),
                    altura_cm = table.Column<int>(type: "integer", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    cns = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    comorbidades = table.Column<List<string>>(type: "text[]", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    deficiencias = table.Column<List<string>>(type: "text[]", nullable: false),
                    escolaridade = table.Column<int>(type: "integer", nullable: false),
                    estado_civil = table.Column<int>(type: "integer", nullable: false),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true),
                    fator_rh = table.Column<int>(type: "integer", nullable: false),
                    medicamentos_continuos = table.Column<List<string>>(type: "text[]", nullable: false),
                    nacionalidade = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    naturalidade = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    nome_da_mae = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    nome_do_pai = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    nome_social = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    ocupacao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    peso_kg = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    plano_saude = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    raca_cor = table.Column<int>(type: "integer", nullable: false),
                    responsavel_legal = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    telefone_celular = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    telefone_residencial = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    tipo_sanguineo = table.Column<int>(type: "integer", nullable: false),
                    contato_emergencia_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    contato_emergencia_parentesco = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    contato_emergencia_telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    residencia_latitude = table.Column<double>(type: "double precision", nullable: true),
                    residencia_longitude = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paciente", x => x.id);
                    table.ForeignKey(
                        name: "FK_paciente_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "smsmarica",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

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

            migrationBuilder.CreateIndex(
                name: "ix_paciente_excluido_em",
                schema: "smsmarica",
                table: "paciente",
                column: "excluido_em",
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_paciente_usuario_id",
                schema: "smsmarica",
                table: "paciente",
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
                name: "FK_laudo_paciente_paciente_id",
                schema: "smsmarica",
                table: "laudo",
                column: "paciente_id",
                principalSchema: "smsmarica",
                principalTable: "paciente",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_solicitacao_exame_paciente_paciente_id",
                schema: "smsmarica",
                table: "solicitacao_exame",
                column: "paciente_id",
                principalSchema: "smsmarica",
                principalTable: "paciente",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tratamento_paciente_paciente_id",
                schema: "smsmarica",
                table: "tratamento",
                column: "paciente_id",
                principalSchema: "smsmarica",
                principalTable: "paciente",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
