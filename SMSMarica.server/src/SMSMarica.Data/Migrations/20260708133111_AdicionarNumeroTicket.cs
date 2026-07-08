using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarNumeroTicket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "numero",
                schema: "smsmarica",
                table: "ticket",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            // Backfill: a identity preenche os tickets existentes em ordem arbitrária;
            // renumera pela ordem de criação (referência humana fica cronológica) e
            // realinha a sequência para o próximo ticket continuar do maior número.
            migrationBuilder.Sql("""
                WITH ordenado AS (
                    SELECT id, ROW_NUMBER() OVER (ORDER BY criado_em, id) AS rn
                    FROM smsmarica.ticket
                )
                UPDATE smsmarica.ticket t
                SET numero = o.rn
                FROM ordenado o
                WHERE t.id = o.id;

                SELECT setval(
                    pg_get_serial_sequence('smsmarica.ticket', 'numero'),
                    (SELECT COALESCE(MAX(numero), 0) + 1 FROM smsmarica.ticket),
                    false);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ticket_numero",
                schema: "smsmarica",
                table: "ticket",
                column: "numero",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ticket_numero",
                schema: "smsmarica",
                table: "ticket");

            migrationBuilder.DropColumn(
                name: "numero",
                schema: "smsmarica",
                table: "ticket");
        }
    }
}
