using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class EscalaSincronizacaoExecucao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sisreg_escala_sincronizacao_execucao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    disparo = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    escalas_lidas = table.Column<int>(type: "integer", nullable: false),
                    escalas_novas = table.Column<int>(type: "integer", nullable: false),
                    escalas_atualizadas = table.Column<int>(type: "integer", nullable: false),
                    escalas_ausentes = table.Column<int>(type: "integer", nullable: false),
                    linhas_rejeitadas = table.Column<int>(type: "integer", nullable: false),
                    unidades_nao_encontradas = table.Column<int>(type: "integer", nullable: false),
                    requisicoes = table.Column<int>(type: "integer", nullable: false),
                    mensagem_erro = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    iniciado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finalizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    duracao_segundos = table.Column<int>(type: "integer", nullable: true),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    criado_por_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sisreg_escala_sincronizacao_execucao", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sisreg_escala_sincronizacao_execucao_iniciado_em",
                schema: "smsmarica",
                table: "sisreg_escala_sincronizacao_execucao",
                column: "iniciado_em",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sisreg_escala_sincronizacao_execucao",
                schema: "smsmarica");
        }
    }
}
