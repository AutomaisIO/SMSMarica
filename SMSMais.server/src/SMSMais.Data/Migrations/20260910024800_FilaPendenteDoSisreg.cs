using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class FilaPendenteDoSisreg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sisreg_fila_pendente",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_solicitacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    data_solicitacao = table.Column<DateOnly>(type: "date", nullable: true),
                    risco = table.Column<int>(type: "integer", nullable: true),
                    paciente_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    cns = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    nome_mae = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    data_nascimento = table.Column<DateOnly>(type: "date", nullable: true),
                    idade_anos = table.Column<int>(type: "integer", nullable: true),
                    telefone = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    municipio = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    procedimento_nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    procedimento_codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    cid_codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    unidade_solicitante = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    situacao = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    primeiro_visto_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ultimo_visto_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    saiu_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    saiu_para = table.Column<int>(type: "integer", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sisreg_fila_pendente", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_fila_pendente_aberta_por_procedimento",
                schema: "smsmarica",
                table: "sisreg_fila_pendente",
                columns: new[] { "procedimento_nome", "data_solicitacao" },
                filter: "saiu_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_fila_pendente_cns_aberta",
                schema: "smsmarica",
                table: "sisreg_fila_pendente",
                column: "cns",
                filter: "cns IS NOT NULL AND saiu_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_fila_pendente_codigo",
                schema: "smsmarica",
                table: "sisreg_fila_pendente",
                column: "codigo_solicitacao",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sisreg_fila_pendente",
                schema: "smsmarica");
        }
    }
}
