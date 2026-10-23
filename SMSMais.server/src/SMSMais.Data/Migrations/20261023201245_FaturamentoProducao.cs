using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class FaturamentoProducao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "faturamento_producao_unidade",
                schema: "smsmarica",
                columns: table => new
                {
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    habilitada = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_faturamento_producao_unidade", x => x.unidade_id);
                    table.ForeignKey(
                        name: "FK_faturamento_producao_unidade_unidade_unidade_id",
                        column: x => x.unidade_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "faturamento_profissional_cns",
                schema: "smsmarica",
                columns: table => new
                {
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    cns = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    cbo = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_faturamento_profissional_cns", x => x.cpf);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "faturamento_producao_unidade",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "faturamento_profissional_cns",
                schema: "smsmarica");
        }
    }
}
