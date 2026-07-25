using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSisregMapeamentoECredencialUnidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sisreg_credencial_unidade",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    senha_cifrada = table.Column<string>(type: "text", nullable: false),
                    cnes_confirmado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    unidade_sisreg_nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    validado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sisreg_credencial_unidade", x => x.id);
                    table.ForeignKey(
                        name: "FK_sisreg_credencial_unidade_unidade_unidade_id",
                        column: x => x.unidade_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sisreg_profissional_unidade",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    habilitado = table.Column<bool>(type: "boolean", nullable: false),
                    practitioner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sincronizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    visto_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ausente = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sisreg_profissional_unidade", x => x.id);
                    table.ForeignKey(
                        name: "FK_sisreg_profissional_unidade_unidade_unidade_id",
                        column: x => x.unidade_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sisreg_procedimento_profissional",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    profissional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    habilitado = table.Column<bool>(type: "boolean", nullable: false),
                    grupo = table.Column<bool>(type: "boolean", nullable: false),
                    visto_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ausente = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sisreg_procedimento_profissional", x => x.id);
                    table.ForeignKey(
                        name: "FK_sisreg_procedimento_profissional_sisreg_profissional_unidad~",
                        column: x => x.profissional_id,
                        principalSchema: "smsmarica",
                        principalTable: "sisreg_profissional_unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_sisreg_credencial_unidade",
                schema: "smsmarica",
                table: "sisreg_credencial_unidade",
                column: "unidade_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_sisreg_procedimento_profissional_codigo",
                schema: "smsmarica",
                table: "sisreg_procedimento_profissional",
                columns: new[] { "profissional_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_profissional_unidade_habilitado",
                schema: "smsmarica",
                table: "sisreg_profissional_unidade",
                columns: new[] { "unidade_id", "habilitado" });

            migrationBuilder.CreateIndex(
                name: "ux_sisreg_profissional_unidade_cpf",
                schema: "smsmarica",
                table: "sisreg_profissional_unidade",
                columns: new[] { "unidade_id", "cpf" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sisreg_credencial_unidade",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "sisreg_procedimento_profissional",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "sisreg_profissional_unidade",
                schema: "smsmarica");
        }
    }
}
