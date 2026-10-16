using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProcedimentosDaSolicitacaoEExamePorCodigo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_exame_imagem_solicitacao_id",
                schema: "smsmarica",
                table: "exame_imagem");

            migrationBuilder.AddColumn<int>(
                name: "ordem",
                schema: "smsmarica",
                table: "exame_imagem",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "solicitacao_principal_id",
                schema: "smsmarica",
                table: "exame_imagem",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "solicitacao_procedimento_id",
                schema: "smsmarica",
                table: "exame_imagem",
                type: "uuid",
                nullable: true);

            // ADR-0074: até aqui havia no máximo um exame por solicitação (índice único em
            // solicitacao_id), então todo exame existente é o PRINCIPAL do seu pedido. Precisa
            // vir antes do índice único e da trava ck_exame_imagem_principal, que o exigem.
            // 28.553 linhas (12 MB) em 16/10/2026.
            migrationBuilder.Sql(
                "UPDATE smsmarica.exame_imagem SET solicitacao_principal_id = solicitacao_id, ordem = 0;");

            migrationBuilder.AddColumn<int>(
                name: "codigo_procedimento_destinos",
                schema: "smsmarica",
                table: "equipamento",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "codigo_procedimento_esquema",
                schema: "smsmarica",
                table: "equipamento",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "solicitacao_procedimento",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    procedimento_codigo_sisreg = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    procedimento_sigtap_codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    procedimento_texto = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    profissional_executante_cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    profissional_executante_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    data_agendada = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    raw_sisreg = table.Column<string>(type: "text", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_solicitacao_procedimento", x => x.id);
                    table.ForeignKey(
                        name: "FK_solicitacao_procedimento_solicitacao_solicitacao_id",
                        column: x => x.solicitacao_id,
                        principalSchema: "smsmarica",
                        principalTable: "solicitacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exame_imagem_solicitacao_id",
                schema: "smsmarica",
                table: "exame_imagem",
                column: "solicitacao_id");

            migrationBuilder.CreateIndex(
                name: "IX_exame_imagem_solicitacao_id_ordem",
                schema: "smsmarica",
                table: "exame_imagem",
                columns: new[] { "solicitacao_id", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exame_imagem_solicitacao_principal_id",
                schema: "smsmarica",
                table: "exame_imagem",
                column: "solicitacao_principal_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exame_imagem_solicitacao_procedimento_id",
                schema: "smsmarica",
                table: "exame_imagem",
                column: "solicitacao_procedimento_id",
                unique: true,
                filter: "solicitacao_procedimento_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_exame_imagem_principal",
                schema: "smsmarica",
                table: "exame_imagem",
                sql: "(solicitacao_principal_id IS NULL AND ordem > 0) OR (solicitacao_principal_id = solicitacao_id AND ordem = 0)");

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_procedimento_solicitacao_id_ordem",
                schema: "smsmarica",
                table: "solicitacao_procedimento",
                columns: new[] { "solicitacao_id", "ordem" },
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_exame_imagem_solicitacao_procedimento_solicitacao_procedime~",
                schema: "smsmarica",
                table: "exame_imagem",
                column: "solicitacao_procedimento_id",
                principalSchema: "smsmarica",
                principalTable: "solicitacao_procedimento",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_exame_imagem_solicitacao_solicitacao_principal_id",
                schema: "smsmarica",
                table: "exame_imagem",
                column: "solicitacao_principal_id",
                principalSchema: "smsmarica",
                principalTable: "solicitacao",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_exame_imagem_solicitacao_procedimento_solicitacao_procedime~",
                schema: "smsmarica",
                table: "exame_imagem");

            migrationBuilder.DropForeignKey(
                name: "FK_exame_imagem_solicitacao_solicitacao_principal_id",
                schema: "smsmarica",
                table: "exame_imagem");

            migrationBuilder.DropTable(
                name: "solicitacao_procedimento",
                schema: "smsmarica");

            migrationBuilder.DropIndex(
                name: "IX_exame_imagem_solicitacao_id",
                schema: "smsmarica",
                table: "exame_imagem");

            migrationBuilder.DropIndex(
                name: "IX_exame_imagem_solicitacao_id_ordem",
                schema: "smsmarica",
                table: "exame_imagem");

            migrationBuilder.DropIndex(
                name: "IX_exame_imagem_solicitacao_principal_id",
                schema: "smsmarica",
                table: "exame_imagem");

            migrationBuilder.DropIndex(
                name: "IX_exame_imagem_solicitacao_procedimento_id",
                schema: "smsmarica",
                table: "exame_imagem");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exame_imagem_principal",
                schema: "smsmarica",
                table: "exame_imagem");

            migrationBuilder.DropColumn(
                name: "ordem",
                schema: "smsmarica",
                table: "exame_imagem");

            migrationBuilder.DropColumn(
                name: "solicitacao_principal_id",
                schema: "smsmarica",
                table: "exame_imagem");

            migrationBuilder.DropColumn(
                name: "solicitacao_procedimento_id",
                schema: "smsmarica",
                table: "exame_imagem");

            migrationBuilder.DropColumn(
                name: "codigo_procedimento_destinos",
                schema: "smsmarica",
                table: "equipamento");

            migrationBuilder.DropColumn(
                name: "codigo_procedimento_esquema",
                schema: "smsmarica",
                table: "equipamento");

            migrationBuilder.CreateIndex(
                name: "IX_exame_imagem_solicitacao_id",
                schema: "smsmarica",
                table: "exame_imagem",
                column: "solicitacao_id",
                unique: true);
        }
    }
}
