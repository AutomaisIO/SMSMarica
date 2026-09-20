using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class ContatoComprometido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "contato_comprometido",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    telefone_canonical = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo = table.Column<int>(type: "integer", nullable: false),
                    detalhe = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    comunicacao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    descoberto_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ultima_ocorrencia_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ocorrencias = table.Column<int>(type: "integer", nullable: false),
                    resolvido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolvido_por = table.Column<Guid>(type: "uuid", nullable: true),
                    resolucao_nota = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contato_comprometido", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_contato_comprometido_aberto_paciente",
                schema: "smsmarica",
                table: "contato_comprometido",
                columns: new[] { "resolvido_em", "paciente_id" });

            migrationBuilder.CreateIndex(
                name: "ux_contato_comprometido_aberto",
                schema: "smsmarica",
                table: "contato_comprometido",
                columns: new[] { "paciente_id", "telefone_canonical", "motivo" },
                unique: true,
                filter: "resolvido_em IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contato_comprometido",
                schema: "smsmarica");
        }
    }
}
