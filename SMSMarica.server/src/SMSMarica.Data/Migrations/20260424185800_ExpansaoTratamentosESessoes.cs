using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExpansaoTratamentosESessoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_avaliacao_translado_sessao_sessao_id",
                schema: "smsmarica",
                table: "avaliacao");

            migrationBuilder.DropForeignKey(
                name: "FK_translado_alocacao_translado_sessao_sessao_id",
                schema: "smsmarica",
                table: "translado_alocacao");

            migrationBuilder.DropTable(
                name: "translado_sessao",
                schema: "smsmarica");

            migrationBuilder.AddColumn<string>(
                name: "codigo_sus_liberacao",
                schema: "smsmarica",
                table: "tratamento",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "hora_prevista_busca",
                schema: "smsmarica",
                table: "tratamento",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "observacoes",
                schema: "smsmarica",
                table: "tratamento",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tipo_tratamento_id",
                schema: "smsmarica",
                table: "tratamento",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sessao_de_tratamento",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tratamento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_prevista = table.Column<DateOnly>(type: "date", nullable: false),
                    hora_prevista_busca = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    hora_prevista_retorno = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    realizada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    confirmada_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nome_acompanhante = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    parentesco_acompanhante = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    motorista_ida_id = table.Column<Guid>(type: "uuid", nullable: true),
                    veiculo_ida_id = table.Column<Guid>(type: "uuid", nullable: true),
                    hora_saida_residencia = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    hora_chegada_unidade = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    motorista_volta_id = table.Column<Guid>(type: "uuid", nullable: true),
                    veiculo_volta_id = table.Column<Guid>(type: "uuid", nullable: true),
                    hora_saida_unidade = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    hora_chegada_residencia = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    motivo_nao_realizacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessao_de_tratamento", x => x.id);
                    table.ForeignKey(
                        name: "FK_sessao_de_tratamento_tratamento_tratamento_id",
                        column: x => x.tratamento_id,
                        principalSchema: "smsmarica",
                        principalTable: "tratamento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tipo_tratamento",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tipo_tratamento", x => x.id);
                });

            migrationBuilder.InsertData(
                schema: "smsmarica",
                table: "tipo_tratamento",
                columns: new[] { "id", "ativo", "codigo", "criado_em", "nome" },
                values: new object[,]
                {
                    { new Guid("0193d000-0000-7000-a000-000000000001"), true, "hemodialise", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Hemodiálise" },
                    { new Guid("0193d000-0000-7000-a000-000000000002"), true, "radioterapia", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Radioterapia" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_tratamento_ativo",
                schema: "smsmarica",
                table: "tratamento",
                column: "ativo");

            migrationBuilder.CreateIndex(
                name: "IX_tratamento_tipo_tratamento_id",
                schema: "smsmarica",
                table: "tratamento",
                column: "tipo_tratamento_id");

            migrationBuilder.CreateIndex(
                name: "IX_sessao_de_tratamento_data_prevista",
                schema: "smsmarica",
                table: "sessao_de_tratamento",
                column: "data_prevista");

            migrationBuilder.CreateIndex(
                name: "IX_sessao_de_tratamento_status",
                schema: "smsmarica",
                table: "sessao_de_tratamento",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_sessao_de_tratamento_tratamento_id_data_prevista",
                schema: "smsmarica",
                table: "sessao_de_tratamento",
                columns: new[] { "tratamento_id", "data_prevista" });

            migrationBuilder.CreateIndex(
                name: "IX_tipo_tratamento_codigo",
                schema: "smsmarica",
                table: "tipo_tratamento",
                column: "codigo",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_avaliacao_sessao_de_tratamento_sessao_id",
                schema: "smsmarica",
                table: "avaliacao",
                column: "sessao_id",
                principalSchema: "smsmarica",
                principalTable: "sessao_de_tratamento",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_translado_alocacao_sessao_de_tratamento_sessao_id",
                schema: "smsmarica",
                table: "translado_alocacao",
                column: "sessao_id",
                principalSchema: "smsmarica",
                principalTable: "sessao_de_tratamento",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tratamento_tipo_tratamento_tipo_tratamento_id",
                schema: "smsmarica",
                table: "tratamento",
                column: "tipo_tratamento_id",
                principalSchema: "smsmarica",
                principalTable: "tipo_tratamento",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_avaliacao_sessao_de_tratamento_sessao_id",
                schema: "smsmarica",
                table: "avaliacao");

            migrationBuilder.DropForeignKey(
                name: "FK_translado_alocacao_sessao_de_tratamento_sessao_id",
                schema: "smsmarica",
                table: "translado_alocacao");

            migrationBuilder.DropForeignKey(
                name: "FK_tratamento_tipo_tratamento_tipo_tratamento_id",
                schema: "smsmarica",
                table: "tratamento");

            migrationBuilder.DropTable(
                name: "sessao_de_tratamento",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "tipo_tratamento",
                schema: "smsmarica");

            migrationBuilder.DropIndex(
                name: "IX_tratamento_ativo",
                schema: "smsmarica",
                table: "tratamento");

            migrationBuilder.DropIndex(
                name: "IX_tratamento_tipo_tratamento_id",
                schema: "smsmarica",
                table: "tratamento");

            migrationBuilder.DropColumn(
                name: "codigo_sus_liberacao",
                schema: "smsmarica",
                table: "tratamento");

            migrationBuilder.DropColumn(
                name: "hora_prevista_busca",
                schema: "smsmarica",
                table: "tratamento");

            migrationBuilder.DropColumn(
                name: "observacoes",
                schema: "smsmarica",
                table: "tratamento");

            migrationBuilder.DropColumn(
                name: "tipo_tratamento_id",
                schema: "smsmarica",
                table: "tratamento");

            migrationBuilder.CreateTable(
                name: "translado_sessao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tratamento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_prevista = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translado_sessao", x => x.id);
                    table.ForeignKey(
                        name: "FK_translado_sessao_tratamento_tratamento_id",
                        column: x => x.tratamento_id,
                        principalSchema: "smsmarica",
                        principalTable: "tratamento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_translado_sessao_data_prevista",
                schema: "smsmarica",
                table: "translado_sessao",
                column: "data_prevista");

            migrationBuilder.CreateIndex(
                name: "IX_translado_sessao_tratamento_id_data_prevista",
                schema: "smsmarica",
                table: "translado_sessao",
                columns: new[] { "tratamento_id", "data_prevista" });

            migrationBuilder.AddForeignKey(
                name: "FK_avaliacao_translado_sessao_sessao_id",
                schema: "smsmarica",
                table: "avaliacao",
                column: "sessao_id",
                principalSchema: "smsmarica",
                principalTable: "translado_sessao",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_translado_alocacao_translado_sessao_sessao_id",
                schema: "smsmarica",
                table: "translado_alocacao",
                column: "sessao_id",
                principalSchema: "smsmarica",
                principalTable: "translado_sessao",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
