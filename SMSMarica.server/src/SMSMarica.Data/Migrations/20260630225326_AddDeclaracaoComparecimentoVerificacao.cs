using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeclaracaoComparecimentoVerificacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "declaracao_comparecimento_verificacao",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitacao_exame_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_hora_exame = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_declaracao_comparecimento_verificacao", x => x.id);
                    table.ForeignKey(
                        name: "FK_declaracao_comparecimento_verificacao_solicitacao_exame_sol~",
                        column: x => x.solicitacao_exame_id,
                        principalSchema: "smsmarica",
                        principalTable: "solicitacao_exame",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_declaracao_comparecimento_verificacao_solicitacao_exame_id",
                schema: "smsmarica",
                table: "declaracao_comparecimento_verificacao",
                column: "solicitacao_exame_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "declaracao_comparecimento_verificacao",
                schema: "smsmarica");
        }
    }
}
