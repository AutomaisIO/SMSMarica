using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class VerbosExternosAmpliados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remapeia o que a migration FollowUpEstruturado deixou em "Outro" (99) com os verbos
            // vistos em producao em 16/09/2026 ("Chegada no Destino" sozinho eram 12.985 linhas).
            // Mesma regra de ClassificadorEventoRegulacao.TipoDoVerbo, por INICIO do texto.
            // So toca o 99: o que ja estava tipado continua igual.
            foreach (var tabela in new[] { "ser_evento", "sernit_evento" })
            {
                migrationBuilder.Sql($"""
                    UPDATE smsmarica.{tabela}
                    SET tipo_evento = CASE
                        WHEN evento ILIKE 'reagend%'  THEN 10
                        WHEN evento ILIKE 'chegada%'  THEN 6
                        WHEN evento ILIKE 'transfer%' THEN 7
                        WHEN evento ILIKE 'devolvid%' THEN 8
                        WHEN evento ILIKE 'whatsapp%' THEN 9
                        WHEN evento ILIKE 'retornar%' THEN 11
                        WHEN evento ILIKE 'alta%'     THEN 12
                        WHEN evento ILIKE 'dar alta%' THEN 12
                        WHEN evento ILIKE 'corrig%'   THEN 13
                        ELSE 99
                    END
                    WHERE tipo_evento = 99;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Volta os tipos novos para "Outro"; o verbo cru continua em `evento`.
            foreach (var tabela in new[] { "ser_evento", "sernit_evento" })
            {
                migrationBuilder.Sql($"UPDATE smsmarica.{tabela} SET tipo_evento = 99 WHERE tipo_evento BETWEEN 6 AND 13;");
            }
        }
    }
}
