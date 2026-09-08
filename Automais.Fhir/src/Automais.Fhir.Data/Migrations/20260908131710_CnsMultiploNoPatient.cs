using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Fhir.Data.Migrations
{
    /// <inheritdoc />
    public partial class CnsMultiploNoPatient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "cns_todos",
                schema: "fhir",
                table: "patient",
                type: "text[]",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_patient_cns_todos",
                schema: "fhir",
                table: "patient",
                column: "cns_todos")
                .Annotation("Npgsql:IndexMethod", "gin");

            // O BACKFILL DAS FICHAS EXISTENTES NAO MORA AQUI, de proposito.
            //
            // Sao 344 mil linhas com expansao de jsonb (custo estimado ~498k, minutos de
            // execucao). Migration roda dentro do AutoMigrate no deploy -- e neste projeto o
            // AutoMigrate FALHA CALADO: um timeout deixaria a coluna vazia com o codigo ja
            // contando com ela, e ninguem saberia ate a busca por CNS antigo comecar a nao achar
            // paciente.
            //
            // A migration faz so o que e rapido e seguro: cria a coluna e o indice. O
            // preenchimento e operacao propria, deliberada, em lotes e com progresso --
            // `backfill_cns_todos.py`. Ate ele rodar, `cns_todos` fica nulo e a busca cai no
            // comportamento antigo (so o CNS oficial), que e exatamente o de hoje.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_patient_cns_todos",
                schema: "fhir",
                table: "patient");

            migrationBuilder.DropColumn(
                name: "cns_todos",
                schema: "fhir",
                table: "patient");
        }
    }
}
