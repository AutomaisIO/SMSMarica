using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class RespostasRapidas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "resposta_rapida",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    corpo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    categoria = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ordem = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resposta_rapida", x => x.id);
                    table.ForeignKey(
                        name: "FK_resposta_rapida_unidade_unidade_id",
                        column: x => x.unidade_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resposta_rapida_campo",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resposta_rapida_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    rotulo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resposta_rapida_campo", x => x.id);
                    table.ForeignKey(
                        name: "FK_resposta_rapida_campo_resposta_rapida_resposta_rapida_id",
                        column: x => x.resposta_rapida_id,
                        principalSchema: "smsmarica",
                        principalTable: "resposta_rapida",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_resposta_rapida_unidade_id_ordem",
                schema: "smsmarica",
                table: "resposta_rapida",
                columns: new[] { "unidade_id", "ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_resposta_rapida_campo_resposta_rapida_id_nome",
                schema: "smsmarica",
                table: "resposta_rapida_campo",
                columns: new[] { "resposta_rapida_id", "nome" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resposta_rapida_campo",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "resposta_rapida",
                schema: "smsmarica");
        }
    }
}
