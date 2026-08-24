using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSisregVarreduraExecucaoItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sisreg_varredura_execucao_item",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    execucao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    profissional_cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    profissional_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    procedimento_codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    procedimento_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    requisicoes = table.Column<int>(type: "integer", nullable: false),
                    registros_encontrados = table.Column<int>(type: "integer", nullable: false),
                    validos = table.Column<int>(type: "integer", nullable: false),
                    invalidos = table.Column<int>(type: "integer", nullable: false),
                    ja_existiam = table.Column<int>(type: "integer", nullable: false),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sisreg_varredura_execucao_item", x => x.id);
                    table.ForeignKey(
                        name: "FK_sisreg_varredura_execucao_item_sisreg_varredura_execucao_ex~",
                        column: x => x.execucao_id,
                        principalSchema: "smsmarica",
                        principalTable: "sisreg_varredura_execucao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sisreg_varredura_execucao_item_execucao",
                schema: "smsmarica",
                table: "sisreg_varredura_execucao_item",
                column: "execucao_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sisreg_varredura_execucao_item",
                schema: "smsmarica");
        }
    }
}
