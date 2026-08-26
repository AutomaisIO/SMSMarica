using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPendenciaCadastro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pendencia_cadastro",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    telefone_canonical = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    vinculo = table.Column<int>(type: "integer", nullable: false),
                    observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    resolvido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolvido_por = table.Column<Guid>(type: "uuid", nullable: true),
                    resolucao_nota = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pendencia_cadastro", x => x.id);
                    table.ForeignKey(
                        name: "FK_pendencia_cadastro_conversa_conversa_id",
                        column: x => x.conversa_id,
                        principalSchema: "smsmarica",
                        principalTable: "conversa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pendencia_cadastro_conversa_id",
                schema: "smsmarica",
                table: "pendencia_cadastro",
                column: "conversa_id");

            migrationBuilder.CreateIndex(
                name: "ix_pendencia_cadastro_status_criado",
                schema: "smsmarica",
                table: "pendencia_cadastro",
                columns: new[] { "status", "criado_em" });

            migrationBuilder.CreateIndex(
                name: "ix_pendencia_cadastro_telefone",
                schema: "smsmarica",
                table: "pendencia_cadastro",
                column: "telefone_canonical");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pendencia_cadastro",
                schema: "smsmarica");
        }
    }
}
