using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class IndicadoresRegulacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "mandado_judicial",
                schema: "smsmarica",
                table: "sernit_solicitacao",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "mandado_judicial_verificado_em",
                schema: "smsmarica",
                table: "sernit_solicitacao",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "mandado_judicial",
                schema: "smsmarica",
                table: "ser_solicitacao",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "mandado_judicial_verificado_em",
                schema: "smsmarica",
                table: "ser_solicitacao",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sisreg_falta",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_solicitacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    data_execucao = table.Column<DateOnly>(type: "date", nullable: false),
                    hora = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    procedimento = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    unidade_solicitante = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    lido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sisreg_falta", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sisreg_indicador_coleta",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    coletor = table.Column<int>(type: "integer", nullable: false),
                    janela_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    janela_fim = table.Column<DateOnly>(type: "date", nullable: false),
                    escopo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    status = table.Column<int>(type: "integer", nullable: false),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    linhas = table.Column<int>(type: "integer", nullable: true),
                    iniciado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    erro = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sisreg_indicador_coleta", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sisreg_marcacao_cancelada",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_solicitacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cancelado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_marcacao = table.Column<DateOnly>(type: "date", nullable: true),
                    procedimento = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    justificativa = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    lido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sisreg_marcacao_cancelada", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sisreg_ppi_cota",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    competencia = table.Column<DateOnly>(type: "date", nullable: false),
                    codigo_interno = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    codigo_unificado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    procedimento = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    total = table.Column<int>(type: "integer", nullable: false),
                    usada = table.Column<int>(type: "integer", nullable: false),
                    saldo = table.Column<int>(type: "integer", nullable: true),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    lido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sisreg_ppi_cota", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sisreg_solicitacao_desfecho",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_solicitacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    data_solicitacao = table.Column<DateOnly>(type: "date", nullable: true),
                    data_desfecho = table.Column<DateOnly>(type: "date", nullable: true),
                    procedimento = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    unidade_solicitante_cnes = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    justificativa = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    origem = table.Column<int>(type: "integer", nullable: false),
                    lido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sisreg_solicitacao_desfecho", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sernit_solicitacao_mandado_judicial",
                schema: "smsmarica",
                table: "sernit_solicitacao",
                column: "mandado_judicial",
                filter: "mandado_judicial");

            migrationBuilder.CreateIndex(
                name: "ix_ser_solicitacao_mandado_judicial",
                schema: "smsmarica",
                table: "ser_solicitacao",
                column: "mandado_judicial",
                filter: "mandado_judicial");

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_falta_codigo_data",
                schema: "smsmarica",
                table: "sisreg_falta",
                columns: new[] { "codigo_solicitacao", "data_execucao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_falta_data",
                schema: "smsmarica",
                table: "sisreg_falta",
                column: "data_execucao");

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_indicador_coleta_a_fazer",
                schema: "smsmarica",
                table: "sisreg_indicador_coleta",
                column: "status",
                filter: "status in (1, 2, 4)");

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_indicador_coleta_item",
                schema: "smsmarica",
                table: "sisreg_indicador_coleta",
                columns: new[] { "coletor", "janela_inicio", "escopo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_marcacao_cancelada_codigo_instante",
                schema: "smsmarica",
                table: "sisreg_marcacao_cancelada",
                columns: new[] { "codigo_solicitacao", "cancelado_em" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_marcacao_cancelada_instante",
                schema: "smsmarica",
                table: "sisreg_marcacao_cancelada",
                column: "cancelado_em");

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_ppi_cota_competencia_procedimento",
                schema: "smsmarica",
                table: "sisreg_ppi_cota",
                columns: new[] { "competencia", "codigo_interno", "tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_solicitacao_desfecho_codigo_situacao",
                schema: "smsmarica",
                table: "sisreg_solicitacao_desfecho",
                columns: new[] { "codigo_solicitacao", "situacao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_solicitacao_desfecho_data_desfecho",
                schema: "smsmarica",
                table: "sisreg_solicitacao_desfecho",
                column: "data_desfecho");

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_solicitacao_desfecho_data_solicitacao",
                schema: "smsmarica",
                table: "sisreg_solicitacao_desfecho",
                column: "data_solicitacao");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sisreg_falta",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "sisreg_indicador_coleta",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "sisreg_marcacao_cancelada",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "sisreg_ppi_cota",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "sisreg_solicitacao_desfecho",
                schema: "smsmarica");

            migrationBuilder.DropIndex(
                name: "ix_sernit_solicitacao_mandado_judicial",
                schema: "smsmarica",
                table: "sernit_solicitacao");

            migrationBuilder.DropIndex(
                name: "ix_ser_solicitacao_mandado_judicial",
                schema: "smsmarica",
                table: "ser_solicitacao");

            migrationBuilder.DropColumn(
                name: "mandado_judicial",
                schema: "smsmarica",
                table: "sernit_solicitacao");

            migrationBuilder.DropColumn(
                name: "mandado_judicial_verificado_em",
                schema: "smsmarica",
                table: "sernit_solicitacao");

            migrationBuilder.DropColumn(
                name: "mandado_judicial",
                schema: "smsmarica",
                table: "ser_solicitacao");

            migrationBuilder.DropColumn(
                name: "mandado_judicial_verificado_em",
                schema: "smsmarica",
                table: "ser_solicitacao");
        }
    }
}
