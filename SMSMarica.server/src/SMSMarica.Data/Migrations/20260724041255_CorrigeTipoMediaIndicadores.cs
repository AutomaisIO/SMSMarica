using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <summary>
    /// Corrige o tipo dos indicadores de tempo médio.
    ///
    /// A seed inicial mapeou como <c>Razao</c> (numerador ÷ denominador) motores cujo SQL, na
    /// verdade, já devolve a média pronta na coluna <c>valor</c> (<c>AVG(...)</c>). Como esses
    /// SQL não trazem <c>numerador</c>, o cálculo dava 0 na tela. Passam para o tipo
    /// <c>Media = 6</c>, que lê a coluna <c>valor</c> direto. Ver ADR-0022.
    ///
    /// Critério: qualquer indicador com motor cujo SQL expõe a coluna <c>valor</c>.
    /// Idempotente por natureza (reaplica o mesmo UPDATE sem efeito colateral).
    /// </summary>
    /// <inheritdoc />
    public partial class CorrigeTipoMediaIndicadores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET tipo_resultado = 6      -- Media
 WHERE sql IS NOT NULL
   AND tipo_resultado = 1      -- estava como Razao
   AND (sql ILIKE '%) AS valor%' OR sql ILIKE '% AS valor%');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE smsmarica.indicador
   SET tipo_resultado = 1      -- volta para Razao
 WHERE tipo_resultado = 6;
");
        }
    }
}
