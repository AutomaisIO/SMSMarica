using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConfiguracaoDaRegulacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "regulacao_configuracao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    permitir_externo_com_interno = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ponta_pode_escolher_unidade = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ponta_pode_ver_todas_unidades = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    exigir_cpf = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    rotulo_fila = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    sisreg_prazo_edicao_dias = table.Column<int>(type: "integer", nullable: false, defaultValue: 7),
                    busca_corte_distancia = table.Column<decimal>(type: "numeric(4,3)", nullable: false),
                    busca_score_sugestao_pareamento = table.Column<decimal>(type: "numeric(4,3)", nullable: false),
                    anexo_limite_mb = table.Column<int>(type: "integer", nullable: false, defaultValue: 15),
                    anexo_tipos_permitidos = table.Column<string[]>(type: "text[]", nullable: false),
                    regras_followup_json = table.Column<string>(type: "jsonb", nullable: false),
                    nao_sei_padrao = table.Column<int>(type: "integer", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulacao_configuracao", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "regulacao_configuracao",
                schema: "smsmarica");
        }
    }
}
