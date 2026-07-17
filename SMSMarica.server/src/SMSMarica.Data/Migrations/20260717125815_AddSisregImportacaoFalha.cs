using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSisregImportacaoFalha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sisreg_importacao_falha",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_solicitacao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    hash_linha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    linha_raw = table.Column<string>(type: "text", nullable: false),
                    origem = table.Column<int>(type: "integer", nullable: false),
                    motivo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    nome_arquivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    cnes_executante = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    nome_executante = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    nome_paciente = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    procedimento_texto = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    data_agendada = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    unidade_executante_id = table.Column<Guid>(type: "uuid", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    resolvido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolvido_por = table.Column<Guid>(type: "uuid", nullable: true),
                    resolucao_nota = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    solicitacao_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sisreg_importacao_falha", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_falha_resolvido_unidade",
                schema: "smsmarica",
                table: "sisreg_importacao_falha",
                columns: new[] { "resolvido_em", "unidade_executante_id" });

            migrationBuilder.CreateIndex(
                name: "ux_sisreg_falha_codigo_pendente",
                schema: "smsmarica",
                table: "sisreg_importacao_falha",
                column: "codigo_solicitacao",
                unique: true,
                filter: "codigo_solicitacao IS NOT NULL AND resolvido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_sisreg_falha_hash_pendente",
                schema: "smsmarica",
                table: "sisreg_importacao_falha",
                column: "hash_linha",
                unique: true,
                filter: "codigo_solicitacao IS NULL AND resolvido_em IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sisreg_importacao_falha",
                schema: "smsmarica");
        }
    }
}
