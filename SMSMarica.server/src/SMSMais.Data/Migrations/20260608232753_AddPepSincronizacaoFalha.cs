using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPepSincronizacaoFalha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pep_sincronizacao_falha",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    execucao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fonte_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fonte_slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cd_paciente = table.Column<long>(type: "bigint", nullable: false),
                    mensagem = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    resolvido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pep_sincronizacao_falha", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pep_falha_execucao",
                schema: "smsmarica",
                table: "pep_sincronizacao_falha",
                column: "execucao_id");

            migrationBuilder.CreateIndex(
                name: "ix_pep_falha_fonte_resolvido",
                schema: "smsmarica",
                table: "pep_sincronizacao_falha",
                columns: new[] { "fonte_id", "resolvido_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pep_sincronizacao_falha",
                schema: "smsmarica");
        }
    }
}
