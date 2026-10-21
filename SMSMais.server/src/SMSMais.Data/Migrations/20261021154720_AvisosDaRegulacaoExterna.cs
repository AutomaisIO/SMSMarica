using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AvisosDaRegulacaoExterna : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "motivo_desmarque",
                schema: "smsmarica",
                table: "agendamento_externo",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "aviso_regulacao_configuracao",
                schema: "smsmarica",
                columns: table => new
                {
                    sistema = table.Column<int>(type: "integer", nullable: false),
                    lembrete_habilitado = table.Column<bool>(type: "boolean", nullable: false),
                    lembrete_dias_antes = table.Column<int>(type: "integer", nullable: false),
                    aviso_cancelamento_habilitado = table.Column<bool>(type: "boolean", nullable: false),
                    aviso_cancelamento_ligado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aviso_regulacao_configuracao", x => x.sistema);
                    table.CheckConstraint("ck_aviso_regulacao_configuracao_dias", "lembrete_dias_antes BETWEEN 1 AND 30");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aviso_regulacao_configuracao",
                schema: "smsmarica");

            migrationBuilder.DropColumn(
                name: "motivo_desmarque",
                schema: "smsmarica",
                table: "agendamento_externo");
        }
    }
}
