using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSisregMapeamentoLoteRastreio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "procedimentos_vistos_em",
                schema: "smsmarica",
                table: "sisreg_profissional_unidade",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sisreg_mapeamento_lote_execucao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    disparo = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    status = table.Column<int>(type: "integer", nullable: false),
                    unidades_no_sisreg = table.Column<int>(type: "integer", nullable: false),
                    unidades_criadas = table.Column<int>(type: "integer", nullable: false),
                    unidades_com_cnes_preenchido = table.Column<int>(type: "integer", nullable: false),
                    unidades_total = table.Column<int>(type: "integer", nullable: false),
                    unidades_mapeadas = table.Column<int>(type: "integer", nullable: false),
                    unidades_puladas = table.Column<int>(type: "integer", nullable: false),
                    unidades_com_erro = table.Column<int>(type: "integer", nullable: false),
                    profissionais_encontrados = table.Column<int>(type: "integer", nullable: false),
                    profissionais_novos = table.Column<int>(type: "integer", nullable: false),
                    procedimentos_encontrados = table.Column<int>(type: "integer", nullable: false),
                    procedimentos_novos = table.Column<int>(type: "integer", nullable: false),
                    practitioners_criados = table.Column<int>(type: "integer", nullable: false),
                    practitioners_vinculados = table.Column<int>(type: "integer", nullable: false),
                    requisicoes = table.Column<int>(type: "integer", nullable: false),
                    mensagem_erro = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    iniciado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finalizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    duracao_segundos = table.Column<int>(type: "integer", nullable: true),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sisreg_mapeamento_lote_execucao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sisreg_mapeamento_lote_execucao_item",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    execucao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    cnes = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    unidade_criada = table.Column<bool>(type: "boolean", nullable: false),
                    resultado = table.Column<int>(type: "integer", nullable: false),
                    profissionais_encontrados = table.Column<int>(type: "integer", nullable: false),
                    profissionais_novos = table.Column<int>(type: "integer", nullable: false),
                    profissionais_ausentes = table.Column<int>(type: "integer", nullable: false),
                    procedimentos_encontrados = table.Column<int>(type: "integer", nullable: false),
                    procedimentos_novos = table.Column<int>(type: "integer", nullable: false),
                    practitioners_criados = table.Column<int>(type: "integer", nullable: false),
                    practitioners_vinculados = table.Column<int>(type: "integer", nullable: false),
                    requisicoes = table.Column<int>(type: "integer", nullable: false),
                    observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    registrado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sisreg_mapeamento_lote_execucao_item", x => x.id);
                    table.ForeignKey(
                        name: "FK_sisreg_mapeamento_lote_execucao_item_sisreg_mapeamento_lote~",
                        column: x => x.execucao_id,
                        principalSchema: "smsmarica",
                        principalTable: "sisreg_mapeamento_lote_execucao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_mapeamento_lote_execucao_iniciado",
                schema: "smsmarica",
                table: "sisreg_mapeamento_lote_execucao",
                column: "iniciado_em",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_mapeamento_lote_execucao_item_execucao",
                schema: "smsmarica",
                table: "sisreg_mapeamento_lote_execucao_item",
                column: "execucao_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sisreg_mapeamento_lote_execucao_item",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "sisreg_mapeamento_lote_execucao",
                schema: "smsmarica");

            migrationBuilder.DropColumn(
                name: "procedimentos_vistos_em",
                schema: "smsmarica",
                table: "sisreg_profissional_unidade");
        }
    }
}
