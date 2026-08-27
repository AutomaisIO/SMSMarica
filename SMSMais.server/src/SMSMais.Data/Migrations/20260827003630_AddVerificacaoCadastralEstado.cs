using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVerificacaoCadastralEstado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "verificacao_cadastral_estado",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    telefone_canonical = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    comunicacao_paciente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    etapa = table.Column<int>(type: "integer", nullable: false),
                    cpf_digitos_informados = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    tentativas_erradas = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    reorientacoes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verificacao_cadastral_estado", x => x.id);
                    table.ForeignKey(
                        name: "FK_verificacao_cadastral_estado_comunicacao_paciente_comunicac~",
                        column: x => x.comunicacao_paciente_id,
                        principalSchema: "smsmarica",
                        principalTable: "comunicacao_paciente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_verificacao_cadastral_estado_comunicacao_paciente_id",
                schema: "smsmarica",
                table: "verificacao_cadastral_estado",
                column: "comunicacao_paciente_id");

            migrationBuilder.CreateIndex(
                name: "ux_verificacao_cadastral_telefone",
                schema: "smsmarica",
                table: "verificacao_cadastral_estado",
                column: "telefone_canonical",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "verificacao_cadastral_estado",
                schema: "smsmarica");
        }
    }
}
