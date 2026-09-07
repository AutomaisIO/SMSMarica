using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class FilaPreRegulacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_regulacao_solicitacao_procedimento_id",
                schema: "smsmarica",
                table: "regulacao_solicitacao",
                newName: "ix_regulacao_solicitacao_procedimento");

            migrationBuilder.CreateTable(
                name: "regulacao_evento",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    status_anterior = table.Column<int>(type: "integer", nullable: true),
                    status_novo = table.Column<int>(type: "integer", nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    usuario_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    papel = table.Column<int>(type: "integer", nullable: false),
                    unidade_ativa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    sessao_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    diff_json = table.Column<string>(type: "jsonb", nullable: true),
                    detalhe_json = table.Column<string>(type: "jsonb", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulacao_evento", x => x.id);
                    table.ForeignKey(
                        name: "FK_regulacao_evento_regulacao_solicitacao_solicitacao_id",
                        column: x => x.solicitacao_id,
                        principalSchema: "smsmarica",
                        principalTable: "regulacao_solicitacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "regulacao_solicitacao_destino",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sistema = table.Column<int>(type: "integer", nullable: false),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    motivo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    avaliado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulacao_solicitacao_destino", x => x.id);
                    table.ForeignKey(
                        name: "FK_regulacao_solicitacao_destino_regulacao_solicitacao_solicit~",
                        column: x => x.solicitacao_id,
                        principalSchema: "smsmarica",
                        principalTable: "regulacao_solicitacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_regulacao_solicitacao_agente",
                schema: "smsmarica",
                table: "regulacao_solicitacao",
                column: "agente_responsavel_id");

            migrationBuilder.CreateIndex(
                name: "ix_regulacao_solicitacao_status_fluxo",
                schema: "smsmarica",
                table: "regulacao_solicitacao",
                columns: new[] { "status", "fluxo" });

            migrationBuilder.CreateIndex(
                name: "ux_regulacao_solicitacao_espelho_ser",
                schema: "smsmarica",
                table: "regulacao_solicitacao",
                column: "ser_solicitacao_id",
                unique: true,
                filter: "ser_solicitacao_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_regulacao_solicitacao_espelho_sernit",
                schema: "smsmarica",
                table: "regulacao_solicitacao",
                column: "sernit_solicitacao_id",
                unique: true,
                filter: "sernit_solicitacao_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_regulacao_solicitacao_espelho_solicitacao",
                schema: "smsmarica",
                table: "regulacao_solicitacao",
                column: "solicitacao_id",
                unique: true,
                filter: "solicitacao_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_regulacao_solicitacao_nar",
                schema: "smsmarica",
                table: "regulacao_solicitacao",
                sql: "(fluxo = 3) = (unidade_em_nome_de_id IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_regulacao_solicitacao_um_espelho",
                schema: "smsmarica",
                table: "regulacao_solicitacao",
                sql: "((solicitacao_id IS NOT NULL)::int + (ser_solicitacao_id IS NOT NULL)::int + (sernit_solicitacao_id IS NOT NULL)::int) <= 1");

            migrationBuilder.CreateIndex(
                name: "ix_regulacao_evento_solicitacao",
                schema: "smsmarica",
                table: "regulacao_evento",
                columns: new[] { "solicitacao_id", "criado_em" });

            migrationBuilder.CreateIndex(
                name: "ux_regulacao_destino",
                schema: "smsmarica",
                table: "regulacao_solicitacao_destino",
                columns: new[] { "solicitacao_id", "sistema" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "regulacao_evento",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "regulacao_solicitacao_destino",
                schema: "smsmarica");

            migrationBuilder.DropIndex(
                name: "ix_regulacao_solicitacao_agente",
                schema: "smsmarica",
                table: "regulacao_solicitacao");

            migrationBuilder.DropIndex(
                name: "ix_regulacao_solicitacao_status_fluxo",
                schema: "smsmarica",
                table: "regulacao_solicitacao");

            migrationBuilder.DropIndex(
                name: "ux_regulacao_solicitacao_espelho_ser",
                schema: "smsmarica",
                table: "regulacao_solicitacao");

            migrationBuilder.DropIndex(
                name: "ux_regulacao_solicitacao_espelho_sernit",
                schema: "smsmarica",
                table: "regulacao_solicitacao");

            migrationBuilder.DropIndex(
                name: "ux_regulacao_solicitacao_espelho_solicitacao",
                schema: "smsmarica",
                table: "regulacao_solicitacao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_regulacao_solicitacao_nar",
                schema: "smsmarica",
                table: "regulacao_solicitacao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_regulacao_solicitacao_um_espelho",
                schema: "smsmarica",
                table: "regulacao_solicitacao");

            migrationBuilder.RenameIndex(
                name: "ix_regulacao_solicitacao_procedimento",
                schema: "smsmarica",
                table: "regulacao_solicitacao",
                newName: "IX_regulacao_solicitacao_procedimento_id");
        }
    }
}
