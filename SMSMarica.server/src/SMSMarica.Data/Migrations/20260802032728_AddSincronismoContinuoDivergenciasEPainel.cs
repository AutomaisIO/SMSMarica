using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSincronismoContinuoDivergenciasEPainel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "causa",
                schema: "smsmarica",
                table: "sisreg_importacao_falha",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<string>(
                name: "paciente_cns",
                schema: "smsmarica",
                table: "sisreg_importacao_falha",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "disparo",
                schema: "smsmarica",
                table: "pep_sincronizacao_execucao",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<long>(
                name: "ultimo_sync_edoc_log_id",
                schema: "smsmarica",
                table: "pep_sincronizacao_estado",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ultimo_sync_fia_em",
                schema: "smsmarica",
                table: "pep_sincronizacao_estado",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pep_sincronizacao_agenda",
                schema: "smsmarica",
                columns: table => new
                {
                    fonte_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    intervalo_minutos = table.Column<int>(type: "integer", nullable: false),
                    janela_inicio_local = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    janela_fim_local = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    medico_rescan_horas = table.Column<int>(type: "integer", nullable: false),
                    falhas_consecutivas = table.Column<int>(type: "integer", nullable: false),
                    proximo_run_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    pausado_ate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pep_sincronizacao_agenda", x => x.fonte_id);
                });

            migrationBuilder.CreateTable(
                name: "pep_sincronizacao_divergencia",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    execucao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fonte_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fonte_slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cd_paciente = table.Column<long>(type: "bigint", nullable: false),
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    valor_origem = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    valor_hub = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    nome_origem = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    nome_hub = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    patient_id_hub = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    veredicto = table.Column<int>(type: "integer", nullable: false),
                    veredicto_motor = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    valor_correto = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    nome_oficial = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    detalhe = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ocorrencias = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    verificado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolvido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolvido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pep_sincronizacao_divergencia", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_falha_cns",
                schema: "smsmarica",
                table: "sisreg_importacao_falha",
                column: "paciente_cns",
                filter: "resolvido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_pep_divergencia_fonte_cpf",
                schema: "smsmarica",
                table: "pep_sincronizacao_divergencia",
                columns: new[] { "fonte_id", "cpf" });

            migrationBuilder.CreateIndex(
                name: "ix_pep_divergencia_fonte_status",
                schema: "smsmarica",
                table: "pep_sincronizacao_divergencia",
                columns: new[] { "fonte_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_pep_divergencia_fonte_cpf_tipo",
                schema: "smsmarica",
                table: "pep_sincronizacao_divergencia",
                columns: new[] { "fonte_id", "cpf", "tipo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pep_sincronizacao_agenda",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "pep_sincronizacao_divergencia",
                schema: "smsmarica");

            migrationBuilder.DropIndex(
                name: "ix_sisreg_falha_cns",
                schema: "smsmarica",
                table: "sisreg_importacao_falha");

            migrationBuilder.DropColumn(
                name: "causa",
                schema: "smsmarica",
                table: "sisreg_importacao_falha");

            migrationBuilder.DropColumn(
                name: "paciente_cns",
                schema: "smsmarica",
                table: "sisreg_importacao_falha");

            migrationBuilder.DropColumn(
                name: "disparo",
                schema: "smsmarica",
                table: "pep_sincronizacao_execucao");

            migrationBuilder.DropColumn(
                name: "ultimo_sync_edoc_log_id",
                schema: "smsmarica",
                table: "pep_sincronizacao_estado");

            migrationBuilder.DropColumn(
                name: "ultimo_sync_fia_em",
                schema: "smsmarica",
                table: "pep_sincronizacao_estado");
        }
    }
}
