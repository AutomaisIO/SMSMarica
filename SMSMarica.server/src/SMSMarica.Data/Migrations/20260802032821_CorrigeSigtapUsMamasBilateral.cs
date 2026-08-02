using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <summary>
    /// Correção de dado semeado: o procedimento SIGTAP da ultrassonografia de mamas
    /// BILATERAL estava com o código do exame unilateral. Só troca o valor da coluna —
    /// não há mudança de modelo, por isso a migration nasce vazia e o corpo é escrito
    /// à mão (mesmo conteúdo da original `20260728132644`, regerada aqui para o Designer
    /// nascer coerente com o modelo desta entrega).
    /// </summary>
    public partial class CorrigeSigtapUsMamasBilateral : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "smsmarica",
                table: "procedimento_sigtap",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000025"),
                column: "codigo",
                value: "02.05.02.009-7");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "smsmarica",
                table: "procedimento_sigtap",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000025"),
                column: "codigo",
                value: "02.05.02.005-4");
        }
    }
}
