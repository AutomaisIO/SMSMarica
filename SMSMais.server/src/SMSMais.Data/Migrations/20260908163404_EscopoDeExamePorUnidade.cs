using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class EscopoDeExamePorUnidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tipo_exame_unidade",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_exame_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enviar_para_worklist = table.Column<bool>(type: "boolean", nullable: false),
                    equipamento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tipo_exame_unidade", x => x.id);
                    table.ForeignKey(
                        name: "FK_tipo_exame_unidade_equipamento_equipamento_id",
                        column: x => x.equipamento_id,
                        principalSchema: "smsmarica",
                        principalTable: "equipamento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tipo_exame_unidade_tipo_exame_tipo_exame_id",
                        column: x => x.tipo_exame_id,
                        principalSchema: "smsmarica",
                        principalTable: "tipo_exame",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tipo_exame_unidade_unidade_unidade_id",
                        column: x => x.unidade_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tipo_exame_unidade_equipamento_id",
                schema: "smsmarica",
                table: "tipo_exame_unidade",
                column: "equipamento_id");

            migrationBuilder.CreateIndex(
                name: "ix_tipo_exame_unidade_unidade_envio",
                schema: "smsmarica",
                table: "tipo_exame_unidade",
                columns: new[] { "unidade_id", "enviar_para_worklist" },
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_tipo_exame_unidade",
                schema: "smsmarica",
                table: "tipo_exame_unidade",
                columns: new[] { "tipo_exame_id", "unidade_id" },
                unique: true,
                filter: "excluido_em IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tipo_exame_unidade",
                schema: "smsmarica");
        }
    }
}
