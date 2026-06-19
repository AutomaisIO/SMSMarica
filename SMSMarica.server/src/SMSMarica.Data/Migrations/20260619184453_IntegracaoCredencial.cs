using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class IntegracaoCredencial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "integracao_credencial",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provedor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    client_id_cifrado = table.Column<string>(type: "text", nullable: true),
                    client_secret_cifrado = table.Column<string>(type: "text", nullable: true),
                    parametros_json = table.Column<string>(type: "jsonb", nullable: true),
                    redirect_uri = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integracao_credencial", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_integracao_credencial_provedor",
                schema: "smsmarica",
                table: "integracao_credencial",
                column: "provedor",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integracao_credencial",
                schema: "smsmarica");
        }
    }
}
