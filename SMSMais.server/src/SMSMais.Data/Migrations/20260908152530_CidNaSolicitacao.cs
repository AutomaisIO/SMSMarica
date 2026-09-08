using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class CidNaSolicitacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cid_codigo",
                schema: "smsmarica",
                table: "solicitacao",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_solicitacao_cid_codigo",
                schema: "smsmarica",
                table: "solicitacao",
                column: "cid_codigo",
                filter: "cid_codigo IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_solicitacao_cid_codigo",
                schema: "smsmarica",
                table: "solicitacao");

            migrationBuilder.DropColumn(
                name: "cid_codigo",
                schema: "smsmarica",
                table: "solicitacao");
        }
    }
}
