using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlteracoesAgendaSisreg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sisreg_alteracao_agenda",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_solicitacao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    valor_antes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    valor_depois = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    unidade_executante_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unidade_solicitante_id = table.Column<Guid>(type: "uuid", nullable: true),
                    detectada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tratada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tratada_por = table.Column<Guid>(type: "uuid", nullable: true),
                    comunicada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    comunicada_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sisreg_alteracao_agenda", x => x.id);
                    table.ForeignKey(
                        name: "FK_sisreg_alteracao_agenda_solicitacao_solicitacao_id",
                        column: x => x.solicitacao_id,
                        principalSchema: "smsmarica",
                        principalTable: "solicitacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sisreg_alteracao_agenda_solicitacao_id",
                schema: "smsmarica",
                table: "sisreg_alteracao_agenda",
                column: "solicitacao_id");

            migrationBuilder.CreateIndex(
                name: "IX_sisreg_alteracao_agenda_tratada_em_detectada_em",
                schema: "smsmarica",
                table: "sisreg_alteracao_agenda",
                columns: new[] { "tratada_em", "detectada_em" });

            migrationBuilder.CreateIndex(
                name: "IX_sisreg_alteracao_agenda_unidade_executante_id",
                schema: "smsmarica",
                table: "sisreg_alteracao_agenda",
                column: "unidade_executante_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sisreg_alteracao_agenda",
                schema: "smsmarica");
        }
    }
}
