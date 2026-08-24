using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSisregImportacaoLote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "execucao_id",
                schema: "smsmarica",
                table: "sisreg_importacao_falha",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sisreg_importacao_execucao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_arquivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    caminho_no_zip = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    total_registros = table.Column<int>(type: "integer", nullable: false),
                    validos = table.Column<int>(type: "integer", nullable: false),
                    invalidos = table.Column<int>(type: "integer", nullable: false),
                    ja_existiam = table.Column<int>(type: "integer", nullable: false),
                    mensagem = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    iniciado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    concluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    criado_por_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    unidade_executante_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sisreg_importacao_execucao", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_falha_execucao",
                schema: "smsmarica",
                table: "sisreg_importacao_falha",
                column: "execucao_id");

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_execucao_lote",
                schema: "smsmarica",
                table: "sisreg_importacao_execucao",
                column: "lote_id");

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_execucao_unidade_iniciado",
                schema: "smsmarica",
                table: "sisreg_importacao_execucao",
                columns: new[] { "unidade_executante_id", "iniciado_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sisreg_importacao_execucao",
                schema: "smsmarica");

            migrationBuilder.DropIndex(
                name: "ix_sisreg_falha_execucao",
                schema: "smsmarica",
                table: "sisreg_importacao_falha");

            migrationBuilder.DropColumn(
                name: "execucao_id",
                schema: "smsmarica",
                table: "sisreg_importacao_falha");
        }
    }
}
