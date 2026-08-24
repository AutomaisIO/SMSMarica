using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class MigrarAgendadaParaRecebida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Status 'Agendada' (2) descontinuado — o fluxo de envio agora termina em
            // 'Recebida' (8). Migra linhas legadas presas em Agendada para Recebida
            // (sem isso o SincronizadorExamesService, que passou a observar Recebida,
            // não promoveria essas solicitações para Realizada).
            migrationBuilder.Sql("UPDATE smsmarica.solicitacao_exame SET status = 8 WHERE status = 2;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Migração de dados unidirecional — não há reversão segura (nem toda
            // 'Recebida' veio de 'Agendada').
        }
    }
}
