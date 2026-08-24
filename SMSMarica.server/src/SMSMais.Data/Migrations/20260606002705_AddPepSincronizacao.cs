using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPepSincronizacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pep_sincronizacao_estado",
                schema: "smsmarica",
                columns: table => new
                {
                    fonte_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ultimo_sync_medico_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ultimo_sync_paciente_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ultimo_sync_baa_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ultimo_sync_edoc_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pep_sincronizacao_estado", x => x.fonte_id);
                });

            migrationBuilder.CreateTable(
                name: "pep_sincronizacao_execucao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fonte_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fonte_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    modo = table.Column<int>(type: "integer", nullable: false),
                    escopo = table.Column<int>(type: "integer", nullable: false),
                    apagar_antes = table.Column<bool>(type: "boolean", nullable: false),
                    max_medicos = table.Column<int>(type: "integer", nullable: true),
                    max_pacientes = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    iniciado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finalizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    duracao_segundos = table.Column<double>(type: "double precision", nullable: true),
                    medicos = table.Column<int>(type: "integer", nullable: false),
                    pacientes = table.Column<int>(type: "integer", nullable: false),
                    encounters = table.Column<int>(type: "integer", nullable: false),
                    conditions = table.Column<int>(type: "integer", nullable: false),
                    medication_requests = table.Column<int>(type: "integer", nullable: false),
                    document_references = table.Column<int>(type: "integer", nullable: false),
                    observations = table.Column<int>(type: "integer", nullable: false),
                    falhas = table.Column<int>(type: "integer", nullable: false),
                    tempos_json = table.Column<string>(type: "text", nullable: true),
                    falhas_json = table.Column<string>(type: "text", nullable: true),
                    mensagem_erro = table.Column<string>(type: "text", nullable: true),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pep_sincronizacao_execucao", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pep_execucao_fonte_iniciado",
                schema: "smsmarica",
                table: "pep_sincronizacao_execucao",
                columns: new[] { "fonte_id", "iniciado_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pep_sincronizacao_estado",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "pep_sincronizacao_execucao",
                schema: "smsmarica");
        }
    }
}
