using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnamnese : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "anamnese",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitacao_exame_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    conteudo_json = table.Column<string>(type: "jsonb", nullable: false),
                    classificacao_risco = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    preenchido_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    preenchido_por_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anamnese", x => x.id);
                    table.ForeignKey(
                        name: "FK_anamnese_solicitacao_exame_solicitacao_exame_id",
                        column: x => x.solicitacao_exame_id,
                        principalSchema: "smsmarica",
                        principalTable: "solicitacao_exame",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_anamnese_classificacao_risco",
                schema: "smsmarica",
                table: "anamnese",
                column: "classificacao_risco");

            migrationBuilder.CreateIndex(
                name: "ix_anamnese_solicitacao_exame_id_ativa",
                schema: "smsmarica",
                table: "anamnese",
                column: "solicitacao_exame_id",
                unique: true,
                filter: "excluido_em IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anamnese",
                schema: "smsmarica");
        }
    }
}
