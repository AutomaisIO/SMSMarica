using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class TfdFaturamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tfd_config_faturamento",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor_por_50km = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    km_por_unidade = table.Column<int>(type: "integer", nullable: false, defaultValue: 50),
                    codigo_sigtap = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tfd_config_faturamento", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tfd_registro_faturamento",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sessao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    motorista_id = table.Column<Guid>(type: "uuid", nullable: true),
                    veiculo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo_tratamento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competencia = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    km_com_paciente = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    unidades = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    valor_unitario = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    valor_total = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    codigo_sigtap = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tfd_registro_faturamento", x => x.id);
                    table.ForeignKey(
                        name: "FK_tfd_registro_faturamento_sessao_de_tratamento_sessao_id",
                        column: x => x.sessao_id,
                        principalSchema: "smsmarica",
                        principalTable: "sessao_de_tratamento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tfd_registro_faturamento_competencia",
                schema: "smsmarica",
                table: "tfd_registro_faturamento",
                column: "competencia");

            migrationBuilder.CreateIndex(
                name: "IX_tfd_registro_faturamento_sessao_id",
                schema: "smsmarica",
                table: "tfd_registro_faturamento",
                column: "sessao_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tfd_config_faturamento",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "tfd_registro_faturamento",
                schema: "smsmarica");
        }
    }
}
