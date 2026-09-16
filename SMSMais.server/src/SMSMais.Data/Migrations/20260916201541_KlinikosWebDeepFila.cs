using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class KlinikosWebDeepFila : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "klinikos_deep_fila",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provedor = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    spa_codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    unid_codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    prioridade = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    ultima_mensagem = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_klinikos_deep_fila", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_klinikos_deep_drenagem",
                schema: "smsmarica",
                table: "klinikos_deep_fila",
                columns: new[] { "estado", "prioridade", "criado_em" });

            migrationBuilder.CreateIndex(
                name: "ux_klinikos_deep_provedor_spa",
                schema: "smsmarica",
                table: "klinikos_deep_fila",
                columns: new[] { "provedor", "spa_codigo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "klinikos_deep_fila",
                schema: "smsmarica");
        }
    }
}
